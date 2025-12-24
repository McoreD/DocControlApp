using System.Net;
using System.Text.Json;
using System.Linq;
using DocControl.Api.Infrastructure;
using DocControl.Core.Models;
using DocControl.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocControl.Api.Functions;

public sealed class CodesFunctions
{
    private readonly AuthContextFactory authFactory;
    private readonly ProjectMemberRepository memberRepository;
    private readonly CodeSeriesRepository codeSeriesRepository;
    private readonly DocumentRepository documentRepository;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<CodesFunctions> logger;

    public CodesFunctions(
        AuthContextFactory authFactory,
        ProjectMemberRepository memberRepository,
        CodeSeriesRepository codeSeriesRepository,
        DocumentRepository documentRepository,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<CodesFunctions> logger)
    {
        this.authFactory = authFactory;
        this.memberRepository = memberRepository;
        this.codeSeriesRepository = codeSeriesRepository;
        this.documentRepository = documentRepository;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Codes_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/codes")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Access denied");

        var codes = await codeSeriesRepository.ListAsync(projectId, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(codes, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Codes_Upsert")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/codes")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Contributor role required");

        UpsertCodeRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<UpsertCodeRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid code payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Level1) || string.IsNullOrWhiteSpace(payload.Level2) || string.IsNullOrWhiteSpace(payload.Level3))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Level1-3 required");
        }

        var key = new CodeSeriesKey
        {
            ProjectId = projectId,
            Level1 = payload.Level1.Trim(),
            Level2 = payload.Level2.Trim(),
            Level3 = payload.Level3.Trim(),
            Level4 = string.IsNullOrWhiteSpace(payload.Level4) ? null : payload.Level4.Trim()
        };
        var id = await codeSeriesRepository.UpsertAsync(key, payload.Description, payload.NextNumber, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(new { id }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Codes_Delete")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projects/{projectId:long}/codes/{codeSeriesId:long}")] HttpRequestData req,
        long projectId,
        long codeSeriesId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Contributor role required");

        await codeSeriesRepository.DeleteAsync(projectId, codeSeriesId, req.FunctionContext.CancellationToken);
        return req.ToJsonAsync(new { deleted = codeSeriesId }, HttpStatusCode.OK, jsonOptions).Result;
    }

    [Function("Codes_Purge")]
    public async Task<HttpResponseData> PurgeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projects/{projectId:long}/codes/purge")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Owner, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Owner role required");

        // Drop documents first to avoid FK violations, then purge code series.
        var deletedDocs = await documentRepository.PurgeAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var deletedCodes = await codeSeriesRepository.PurgeAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        logger.LogInformation("Purged documents {Docs} and codes {Codes} for project {Project}", deletedDocs, deletedCodes, projectId);
        return await req.ToJsonAsync(new { deletedDocuments = deletedDocs, deletedCodes }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Codes_ExportJson")]
    public async Task<HttpResponseData> ExportAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/codes/export")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Access denied");

        var codes = await codeSeriesRepository.ListAsync(projectId, req.FunctionContext.CancellationToken);
        var payload = codes.Select(c => new
        {
            c.Key.Level1,
            c.Key.Level2,
            c.Key.Level3,
            c.Key.Level4,
            c.Description,
            c.NextNumber
        });
        return await req.ToJsonAsync(payload, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Codes_ImportJson")]
    public async Task<HttpResponseData> ImportJsonAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/codes/import/json")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Contributor role required");

        List<UpsertCodeRequest>? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<List<UpsertCodeRequest>>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid code import JSON payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || payload.Count == 0) return await req.ErrorAsync(HttpStatusCode.BadRequest, "No codes provided");

        var imported = 0;
        foreach (var p in payload)
        {
            if (string.IsNullOrWhiteSpace(p.Level1) || string.IsNullOrWhiteSpace(p.Level2) || string.IsNullOrWhiteSpace(p.Level3)) continue;
            var key = new CodeSeriesKey
            {
                ProjectId = projectId,
                Level1 = p.Level1.Trim(),
                Level2 = p.Level2.Trim(),
                Level3 = p.Level3.Trim(),
                Level4 = string.IsNullOrWhiteSpace(p.Level4) ? null : p.Level4.Trim()
            };
            await codeSeriesRepository.UpsertAsync(key, p.Description, p.NextNumber, req.FunctionContext.CancellationToken);
            imported++;
        }

        return await req.ToJsonAsync(new { imported }, HttpStatusCode.OK, jsonOptions);
    }

    private async Task<bool> IsAtLeast(long projectId, long userId, string requiredRole, CancellationToken cancellationToken)
    {
        var role = await memberRepository.GetRoleAsync(projectId, userId, cancellationToken).ConfigureAwait(false);
        if (role is null) return false;
        return Roles.Compare(role, requiredRole) >= 0;
    }
}

public sealed record UpsertCodeRequest(string Level1, string Level2, string Level3, string? Level4, string? Description, int? NextNumber);

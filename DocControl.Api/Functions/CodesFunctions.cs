using System.Net;
using System.Text.Json;
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
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<CodesFunctions> logger;

    public CodesFunctions(
        AuthContextFactory authFactory,
        ProjectMemberRepository memberRepository,
        CodeSeriesRepository codeSeriesRepository,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<CodesFunctions> logger)
    {
        this.authFactory = authFactory;
        this.memberRepository = memberRepository;
        this.codeSeriesRepository = codeSeriesRepository;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Codes_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/codes")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return req.Error(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Access denied");

        var codes = await codeSeriesRepository.ListAsync(projectId, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(codes, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Codes_Upsert")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/codes")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return req.Error(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Contributor role required");

        UpsertCodeRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<UpsertCodeRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid code payload");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Level1) || string.IsNullOrWhiteSpace(payload.Level2) || string.IsNullOrWhiteSpace(payload.Level3))
        {
            return req.Error(HttpStatusCode.BadRequest, "Level1-3 required");
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
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return req.Error(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Contributor role required");

        await codeSeriesRepository.DeleteAsync(projectId, codeSeriesId, req.FunctionContext.CancellationToken);
        return req.ToJsonAsync(new { deleted = codeSeriesId }, HttpStatusCode.OK, jsonOptions).Result;
    }

    private async Task<bool> IsAtLeast(long projectId, long userId, string requiredRole, CancellationToken cancellationToken)
    {
        var role = await memberRepository.GetRoleAsync(projectId, userId, cancellationToken).ConfigureAwait(false);
        if (role is null) return false;
        return Roles.Compare(role, requiredRole) >= 0;
    }
}

public sealed record UpsertCodeRequest(string Level1, string Level2, string Level3, string? Level4, string? Description, int? NextNumber);

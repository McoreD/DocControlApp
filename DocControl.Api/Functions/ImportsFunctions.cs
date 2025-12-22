using System.Net;
using System.Text.Json;
using DocControl.Api.Infrastructure;
using DocControl.Core.Models;
using DocControl.Core.Configuration;
using DocControl.Infrastructure.Data;
using DocControl.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocControl.Api.Functions;

public sealed class ImportsFunctions
{
    private readonly AuthContextFactory authFactory;
    private readonly ProjectMemberRepository memberRepository;
    private readonly CodeSeriesRepository codeSeriesRepository;
    private readonly DocumentRepository documentRepository;
    private readonly CodeImportService codeImportService;
    private readonly ConfigService configService;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<ImportsFunctions> logger;

    public ImportsFunctions(
        AuthContextFactory authFactory,
        ProjectMemberRepository memberRepository,
        CodeSeriesRepository codeSeriesRepository,
        DocumentRepository documentRepository,
        CodeImportService codeImportService,
        ConfigService configService,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<ImportsFunctions> logger)
    {
        this.authFactory = authFactory;
        this.memberRepository = memberRepository;
        this.codeSeriesRepository = codeSeriesRepository;
        this.documentRepository = documentRepository;
        this.codeImportService = codeImportService;
        this.configService = configService;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Codes_ImportCsv")]
    public async Task<HttpResponseData> ImportCodesCsvAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/codes/import")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Contributor role required");

        var csv = await new StreamReader(req.Body).ReadToEndAsync();
        var result = await codeImportService.ImportCodesFromCsvAsync(projectId, csv, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(result, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Documents_Import")]
    public async Task<HttpResponseData> ImportDocumentsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/documents/import")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Contributor role required");

        ImportDocumentsRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<ImportDocumentsRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid document import payload");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || payload.Entries.Count == 0)
        {
            return req.Error(HttpStatusCode.BadRequest, "No entries");
        }

        var config = await configService.LoadDocumentConfigAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var parser = new FileNameParser(config);
        var now = DateTime.UtcNow;
        var imported = 0;
        var errors = new List<string>();

        foreach (var entry in payload.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Code))
            {
                errors.Add("Empty code");
                continue;
            }

            if (!TryParseCode(entry.Code, config, out var key, out var number, out var reason))
            {
                errors.Add($"{entry.Code}: {reason}");
                continue;
            }

            key = new CodeSeriesKey
            {
                ProjectId = projectId,
                Level1 = key.Level1,
                Level2 = key.Level2,
                Level3 = key.Level3,
                Level4 = key.Level4
            };
            await codeSeriesRepository.UpsertAsync(key, entry.Description ?? string.Empty, number, req.FunctionContext.CancellationToken);
            await documentRepository.UpsertImportedAsync(key, number, entry.FreeText ?? string.Empty, entry.FileName ?? entry.Code, auth.UserId, now, req.FunctionContext.CancellationToken);
            imported++;
        }

        return await req.ToJsonAsync(new { imported, errors }, HttpStatusCode.OK, jsonOptions);
    }

    private static bool TryParseCode(string code, DocumentConfig config, out CodeSeriesKey key, out int number, out string reason)
    {
        key = new CodeSeriesKey { ProjectId = 0, Level1 = string.Empty, Level2 = string.Empty, Level3 = string.Empty, Level4 = null };
        number = 0;
        reason = string.Empty;

        var parts = code.Split(config.Separator, StringSplitOptions.RemoveEmptyEntries);
        if (config.EnableLevel4)
        {
            if (parts.Length != 5) { reason = "Expected Level1-4 and number"; return false; }
            if (!int.TryParse(parts[4], out number)) { reason = "Number not numeric"; return false; }
            key = new CodeSeriesKey { ProjectId = 0, Level1 = parts[0], Level2 = parts[1], Level3 = parts[2], Level4 = parts[3] };
            return true;
        }
        else
        {
            if (parts.Length != 4) { reason = "Expected Level1-3 and number"; return false; }
            if (!int.TryParse(parts[3], out number)) { reason = "Number not numeric"; return false; }
            key = new CodeSeriesKey { ProjectId = 0, Level1 = parts[0], Level2 = parts[1], Level3 = parts[2], Level4 = null };
            return true;
        }
    }

    private async Task<bool> IsAtLeast(long projectId, long userId, string requiredRole, CancellationToken cancellationToken)
    {
        var role = await memberRepository.GetRoleAsync(projectId, userId, cancellationToken).ConfigureAwait(false);
        if (role is null) return false;
        return Roles.Compare(role, requiredRole) >= 0;
    }
}

public sealed record ImportDocumentsRequest(IReadOnlyList<ImportDocumentEntry> Entries);
public sealed record ImportDocumentEntry(string Code, string? FileName, string? FreeText, string? Description);

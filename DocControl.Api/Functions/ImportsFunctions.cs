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
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Contributor role required");

        var raw = await new StreamReader(req.Body).ReadToEndAsync();
        var csv = NormalizeCsvPayload(raw);
        if (string.IsNullOrWhiteSpace(csv)) return await req.ErrorAsync(HttpStatusCode.BadRequest, "CSV payload empty");
        var result = await codeImportService.ImportCodesFromCsvAsync(projectId, csv, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(result, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Documents_Import")]
    public async Task<HttpResponseData> ImportDocumentsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/documents/import")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Contributor role required");

        ImportDocumentsRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<ImportDocumentsRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid document import payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || payload.Entries.Count == 0)
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "No entries");
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
            var description = !string.IsNullOrWhiteSpace(entry.Description)
                ? entry.Description
                : (!string.IsNullOrWhiteSpace(entry.FreeText) ? entry.FreeText : null);
            await codeSeriesRepository.UpsertAsync(key, description, number + 1, req.FunctionContext.CancellationToken);
            await documentRepository.UpsertImportedAsync(key, number, entry.FreeText ?? string.Empty, entry.FileName ?? entry.Code, auth.UserId, now, req.FunctionContext.CancellationToken);
            imported++;
        }

        return await req.ToJsonAsync(new { imported, errors }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Documents_ImportCsv")]
    public async Task<HttpResponseData> ImportDocumentsCsvAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/documents/import/csv")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Contributor role required");

        var config = await configService.LoadDocumentConfigAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var csv = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(csv)) return await req.ErrorAsync(HttpStatusCode.BadRequest, "CSV payload empty");

        var lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var dataLines = lines.Length > 0 && lines[0].StartsWith("Code", StringComparison.OrdinalIgnoreCase) ? lines.Skip(1) : lines;
        var now = DateTime.UtcNow;
        var imported = 0;
        var errors = new List<string>();
        foreach (var line in dataLines)
        {
            var parts = line.Split(',', 2);
            if (parts.Length < 1) continue;
            var codeRaw = parts[0].Trim();
            var freeText = parts.Length > 1 ? parts[1].Trim().Trim('"') : string.Empty;
            if (string.IsNullOrWhiteSpace(codeRaw)) { errors.Add("Empty code"); continue; }
            if (!TryParseCode(codeRaw, config, out var key, out var number, out var reason))
            {
                errors.Add($"{codeRaw}: {reason}");
                continue;
            }
            key = new CodeSeriesKey { ProjectId = projectId, Level1 = key.Level1, Level2 = key.Level2, Level3 = key.Level3, Level4 = key.Level4 };
            var description = string.IsNullOrWhiteSpace(freeText) ? null : freeText;
            await codeSeriesRepository.UpsertAsync(key, description, number + 1, req.FunctionContext.CancellationToken);
            await documentRepository.UpsertImportedAsync(key, number, freeText, codeRaw, auth.UserId, now, req.FunctionContext.CancellationToken);
            imported++;
        }

        return await req.ToJsonAsync(new { imported, errors }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Documents_ImportJson")]
    public async Task<HttpResponseData> ImportDocumentsJsonAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/documents/import/json")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Contributor role required");

        var config = await configService.LoadDocumentConfigAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        IReadOnlyList<ImportDocumentEntry>? entries = null;
        try
        {
            // Accept either { entries: [...] } or raw array
            using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: req.FunctionContext.CancellationToken);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                entries = doc.RootElement.Deserialize<IReadOnlyList<ImportDocumentEntry>>(jsonOptions);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("entries", out var entriesProp))
            {
                entries = entriesProp.Deserialize<IReadOnlyList<ImportDocumentEntry>>(jsonOptions);
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid document import JSON payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (entries is null || entries.Count == 0) return await req.ErrorAsync(HttpStatusCode.BadRequest, "No entries");

        var now = DateTime.UtcNow;
        var imported = 0;
        var errors = new List<string>();
        foreach (var entry in entries)
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
            key = new CodeSeriesKey { ProjectId = projectId, Level1 = key.Level1, Level2 = key.Level2, Level3 = key.Level3, Level4 = key.Level4 };
            var description = !string.IsNullOrWhiteSpace(entry.Description)
                ? entry.Description
                : (!string.IsNullOrWhiteSpace(entry.FreeText) ? entry.FreeText : null);
            await codeSeriesRepository.UpsertAsync(key, description, number + 1, req.FunctionContext.CancellationToken);
            await documentRepository.UpsertImportedAsync(key, number, entry.FreeText ?? string.Empty, entry.FileName ?? entry.Code, auth.UserId, now, req.FunctionContext.CancellationToken);
            imported++;
        }

        return await req.ToJsonAsync(new { imported, errors }, HttpStatusCode.OK, jsonOptions);
    }

    private static string NormalizeCsvPayload(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var trimmed = body.Trim();

        // Allow JSON string payloads (body is just a JSON encoded string)
        if (trimmed.Length >= 2 && trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
        {
            try
            {
                return JsonSerializer.Deserialize<string>(trimmed) ?? string.Empty;
            }
            catch
            {
                // Fall back to raw body
            }
        }

        // Allow { "csv": "..." } style payloads
        if (trimmed.StartsWith("{"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("csv", out var csvProp) &&
                    csvProp.ValueKind == JsonValueKind.String)
                {
                    return csvProp.GetString() ?? string.Empty;
                }
            }
            catch
            {
                // Fall back to raw body
            }
        }

        return body;
    }

    private static bool TryParseCode(string code, DocumentConfig config, out CodeSeriesKey key, out int number, out string reason)
    {
        key = new CodeSeriesKey { ProjectId = 0, Level1 = string.Empty, Level2 = string.Empty, Level3 = string.Empty, Level4 = null };
        number = 0;
        reason = string.Empty;

        var parts = code.Split(config.Separator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 5)
        {
            if (!int.TryParse(parts[4], out number)) { reason = "Number not numeric"; return false; }
            key = new CodeSeriesKey { ProjectId = 0, Level1 = parts[0], Level2 = parts[1], Level3 = parts[2], Level4 = parts[3] };
            return true;
        }
        if (parts.Length == 4)
        {
            if (!int.TryParse(parts[3], out number)) { reason = "Number not numeric"; return false; }
            key = new CodeSeriesKey { ProjectId = 0, Level1 = parts[0], Level2 = parts[1], Level3 = parts[2], Level4 = null };
            return true;
        }

        reason = "Expected Level1-3 or Level1-4 and number";
        return false;
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

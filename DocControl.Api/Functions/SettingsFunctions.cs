using System.Net;
using System.Text.Json;
using DocControl.Api.Infrastructure;
using DocControl.Core.Configuration;
using DocControl.Infrastructure.Data;
using DocControl.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocControl.Api.Functions;

public sealed class SettingsFunctions
{
    private readonly AuthContextFactory authFactory;
    private readonly ConfigService configService;
    private readonly ProjectRepository projectRepository;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<SettingsFunctions> logger;

    public SettingsFunctions(
        AuthContextFactory authFactory,
        ConfigService configService,
        ProjectRepository projectRepository,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<SettingsFunctions> logger)
    {
        this.authFactory = authFactory;
        this.configService = configService;
        this.projectRepository = projectRepository;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Settings_Get")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/settings")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");

        if (!await projectRepository.IsMemberAsync(projectId, auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false))
        {
            return await req.ErrorAsync(HttpStatusCode.Forbidden, "Not a project member.");
        }

        var aiSettings = await configService.LoadAiSettingsAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var (hasOpenAi, hasGemini) = await configService.GetAiKeyStatusAsync(aiSettings, auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var (openAiSuffix, geminiSuffix) = await configService.GetAiKeySuffixesAsync(auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);

        return await req.ToJsonAsync(new ProjectSettingsResponse(aiSettings, hasOpenAi, hasGemini, openAiSuffix, geminiSuffix), HttpStatusCode.OK, jsonOptions);
    }

    [Function("Settings_Save")]
    public async Task<HttpResponseData> SaveAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/settings")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");

        if (!await projectRepository.IsMemberAsync(projectId, auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false))
        {
            return await req.ErrorAsync(HttpStatusCode.Forbidden, "Not a project member.");
        }

        SaveProjectSettingsRequest? payload;
        try
        {
            var raw = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return await req.ErrorAsync(HttpStatusCode.BadRequest, "Empty payload.");
            }
            payload = JsonSerializer.Deserialize<SaveProjectSettingsRequest>(raw, jsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid settings payload.");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, $"Invalid JSON payload: {ex.Message}");
        }

        if (payload?.AiSettings is null)
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "AiSettings are required.");
        }

        try
        {
            await configService.SaveAiSettingsAsync(
                projectId,
                auth.UserId,
                payload.AiSettings,
                payload.OpenAiKey ?? string.Empty,
                payload.GeminiKey ?? string.Empty,
                payload.ClearOpenAiKey,
                payload.ClearGeminiKey,
                req.FunctionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, ex.Message);
        }

        var (hasOpenAi, hasGemini) = await configService.GetAiKeyStatusAsync(payload.AiSettings, auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var (openAiSuffix, geminiSuffix) = await configService.GetAiKeySuffixesAsync(auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        return await req.ToJsonAsync(new { status = "ok", hasOpenAi, hasGemini, openAiKeySuffix = openAiSuffix, geminiKeySuffix = geminiSuffix }, HttpStatusCode.OK, jsonOptions);
    }
}

public sealed record ProjectSettingsResponse(AiSettings AiSettings, bool HasOpenAiKey, bool HasGeminiKey, string? OpenAiKeySuffix, string? GeminiKeySuffix);

public sealed record SaveProjectSettingsRequest(
    AiSettings AiSettings,
    string? OpenAiKey,
    string? GeminiKey,
    bool ClearOpenAiKey,
    bool ClearGeminiKey);

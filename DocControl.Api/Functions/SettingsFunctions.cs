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
    private readonly ConfigService configService;
    private readonly ProjectRepository projectRepository;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<SettingsFunctions> logger;

    public SettingsFunctions(
        ConfigService configService,
        ProjectRepository projectRepository,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<SettingsFunctions> logger)
    {
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
        if (!ProjectsFunctions.TryGetUserId(req, out var userId, out var error))
        {
            return req.Error(HttpStatusCode.Unauthorized, error);
        }

        if (!await projectRepository.IsMemberAsync(projectId, userId, req.FunctionContext.CancellationToken).ConfigureAwait(false))
        {
            return req.Error(HttpStatusCode.Forbidden, "Not a project member.");
        }

        var documentConfig = await configService.LoadDocumentConfigAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var aiSettings = await configService.LoadAiSettingsAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);

        return await req.ToJsonAsync(new ProjectSettingsResponse(documentConfig, aiSettings), HttpStatusCode.OK, jsonOptions);
    }

    [Function("Settings_Save")]
    public async Task<HttpResponseData> SaveAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/settings")] HttpRequestData req,
        long projectId)
    {
        if (!ProjectsFunctions.TryGetUserId(req, out var userId, out var error))
        {
            return req.Error(HttpStatusCode.Unauthorized, error);
        }

        if (!await projectRepository.IsMemberAsync(projectId, userId, req.FunctionContext.CancellationToken).ConfigureAwait(false))
        {
            return req.Error(HttpStatusCode.Forbidden, "Not a project member.");
        }

        SaveProjectSettingsRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<SaveProjectSettingsRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid settings payload.");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload.");
        }

        if (payload?.DocumentConfig is null || payload.AiSettings is null)
        {
            return req.Error(HttpStatusCode.BadRequest, "DocumentConfig and AiSettings are required.");
        }

        payload.DocumentConfig.LevelCount = payload.DocumentConfig.EnableLevel4 ? 4 : 3;

        await configService.SaveDocumentConfigAsync(projectId, payload.DocumentConfig, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        await configService.SaveAiSettingsAsync(projectId, payload.AiSettings, payload.OpenAiKey ?? string.Empty, payload.GeminiKey ?? string.Empty, req.FunctionContext.CancellationToken).ConfigureAwait(false);

        return await req.ToJsonAsync(new { status = "ok" }, HttpStatusCode.OK, jsonOptions);
    }
}

public sealed record ProjectSettingsResponse(DocumentConfig DocumentConfig, AiSettings AiSettings);

public sealed record SaveProjectSettingsRequest(DocumentConfig DocumentConfig, AiSettings AiSettings, string? OpenAiKey, string? GeminiKey);

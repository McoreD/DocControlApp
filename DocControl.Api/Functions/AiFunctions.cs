using System.Net;
using System.Text.Json;
using DocControl.Api.Infrastructure;
using DocControl.Infrastructure.Data;
using DocControl.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocControl.Api.Functions;

public sealed class AiFunctions
{
    private readonly AuthContextFactory authFactory;
    private readonly ProjectMemberRepository memberRepository;
    private readonly CodeCatalogRepository codeCatalogRepository;
    private readonly AiOrchestratorFactory aiFactory;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<AiFunctions> logger;

    public AiFunctions(
        AuthContextFactory authFactory,
        ProjectMemberRepository memberRepository,
        CodeCatalogRepository codeCatalogRepository,
        AiOrchestratorFactory aiFactory,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<AiFunctions> logger)
    {
        this.authFactory = authFactory;
        this.memberRepository = memberRepository;
        this.codeCatalogRepository = codeCatalogRepository;
        this.aiFactory = aiFactory;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("AI_Interpret")]
    public async Task<HttpResponseData> InterpretAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/ai/interpret")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Access denied");

        var orchestrator = await aiFactory.CreateAsync(projectId, auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        if (orchestrator is null) return await req.ErrorAsync(HttpStatusCode.BadRequest, "AI keys not configured for this project");

        QueryRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<QueryRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid AI interpret payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Query))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Query required");
        }

        var nlq = new NlqService(orchestrator);
        try
        {
            var result = await nlq.InterpretAsync(payload.Query, req.FunctionContext.CancellationToken).ConfigureAwait(false);
            return await req.ToJsonAsync(result, HttpStatusCode.OK, jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI interpret failed.");

            return await req.ErrorAsync(HttpStatusCode.BadGateway, "AI interpret failed");
        }
    }

    [Function("AI_Recommend")]
    public async Task<HttpResponseData> RecommendAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/ai/recommend")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return await req.ErrorAsync(HttpStatusCode.Forbidden, "Access denied");

        var orchestrator = await aiFactory.CreateAsync(projectId, auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        if (orchestrator is null) return await req.ErrorAsync(HttpStatusCode.BadRequest, "AI keys not configured for this project");

        QueryRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<QueryRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid AI recommend payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Query))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Query required");
        }

        var codes = await codeCatalogRepository.ListAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var display = codes.Select(c =>
        {
            int level;
            string code;
            if (!string.IsNullOrWhiteSpace(c.Key.Level6))
            {
                level = 6;
                code = c.Key.Level6!;
            }
            else if (!string.IsNullOrWhiteSpace(c.Key.Level5))
            {
                level = 5;
                code = c.Key.Level5!;
            }
            else if (!string.IsNullOrWhiteSpace(c.Key.Level4))
            {
                level = 4;
                code = c.Key.Level4!;
            }
            else if (!string.IsNullOrWhiteSpace(c.Key.Level3))
            {
                level = 3;
                code = c.Key.Level3;
            }
            else if (!string.IsNullOrWhiteSpace(c.Key.Level2))
            {
                level = 2;
                code = c.Key.Level2;
            }
            else
            {
                level = 1;
                code = c.Key.Level1;
            }

            return (Level: level, Code: code, Description: c.Description ?? string.Empty);
        }).ToList();

        var nlq = new NlqService(orchestrator);
        try
        {
            var result = await nlq.RecommendCodeAsync(payload.Query, display, req.FunctionContext.CancellationToken).ConfigureAwait(false);
            return await req.ToJsonAsync(result, HttpStatusCode.OK, jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI recommend failed.");

            return await req.ErrorAsync(HttpStatusCode.BadGateway, "AI recommend failed");
        }
    }

    private async Task<bool> IsAtLeast(long projectId, long userId, string requiredRole, CancellationToken cancellationToken)
    {
        var role = await memberRepository.GetRoleAsync(projectId, userId, cancellationToken).ConfigureAwait(false);
        if (role is null) return false;
        return Roles.Compare(role, requiredRole) >= 0;
    }
}

public sealed record QueryRequest(string Query);

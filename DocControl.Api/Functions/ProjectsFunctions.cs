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

public sealed class ProjectsFunctions
{
    private readonly AuthContextFactory authFactory;
    private readonly ProjectRepository projectRepository;
    private readonly ProjectMemberRepository memberRepository;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<ProjectsFunctions> logger;

    public ProjectsFunctions(
        AuthContextFactory authFactory,
        ProjectRepository projectRepository,
        ProjectMemberRepository memberRepository,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<ProjectsFunctions> logger)
    {
        this.authFactory = authFactory;
        this.projectRepository = projectRepository;
        this.memberRepository = memberRepository;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Projects_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects")] HttpRequestData req)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");

        var projects = await projectRepository.ListForUserAsync(auth.UserId);
        return await req.ToJsonAsync(projects, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Projects_Get")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");

        var project = await projectRepository.GetAsync(projectId, auth.UserId);
        if (project is null)
        {
            return await req.ErrorAsync(HttpStatusCode.NotFound, "Project not found or access denied.");
        }

        return await req.ToJsonAsync(project, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Projects_Create")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequestData req)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");

        CreateProjectRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<CreateProjectRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid create project payload.");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Name))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Name is required.");
        }

        var separator = string.IsNullOrWhiteSpace(payload.Separator) ? "-" : payload.Separator.Trim();
        var padding = payload.PaddingLength <= 0 ? 3 : payload.PaddingLength;
        var levelCount = payload.LevelCount is < 1 or > 6 ? 3 : payload.LevelCount;
        var level1Label = string.IsNullOrWhiteSpace(payload.Level1Label) ? "Level1" : payload.Level1Label.Trim();
        var level2Label = string.IsNullOrWhiteSpace(payload.Level2Label) ? "Level2" : payload.Level2Label.Trim();
        var level3Label = string.IsNullOrWhiteSpace(payload.Level3Label) ? "Level3" : payload.Level3Label.Trim();
        var level4Label = string.IsNullOrWhiteSpace(payload.Level4Label) ? "Level4" : payload.Level4Label.Trim();
        var level5Label = string.IsNullOrWhiteSpace(payload.Level5Label) ? "Level5" : payload.Level5Label.Trim();
        var level6Label = string.IsNullOrWhiteSpace(payload.Level6Label) ? "Level6" : payload.Level6Label.Trim();
        var level1Length = payload.Level1Length is < 1 or > 4 ? 3 : payload.Level1Length;
        var level2Length = payload.Level2Length is < 1 or > 4 ? 3 : payload.Level2Length;
        var level3Length = payload.Level3Length is < 1 or > 4 ? 3 : payload.Level3Length;
        var level4Length = payload.Level4Length is < 1 or > 4 ? 3 : payload.Level4Length;
        var level5Length = payload.Level5Length is < 1 or > 4 ? 3 : payload.Level5Length;
        var level6Length = payload.Level6Length is < 1 or > 4 ? 3 : payload.Level6Length;

        var projectId = await projectRepository.CreateAsync(
            payload.Name.Trim(),
            payload.Description ?? string.Empty,
            separator,
            padding,
            levelCount,
            level1Label,
            level2Label,
            level3Label,
            level4Label,
            level5Label,
            level6Label,
            level1Length,
            level2Length,
            level3Length,
            level4Length,
            level5Length,
            level6Length,
            auth.UserId,
            req.FunctionContext.CancellationToken);
        var created = new ProjectRecord
        {
            Id = projectId,
            Name = payload.Name.Trim(),
            Description = payload.Description ?? string.Empty,
            Separator = separator,
            PaddingLength = padding,
            LevelCount = levelCount,
            Level1Label = level1Label,
            Level2Label = level2Label,
            Level3Label = level3Label,
            Level4Label = level4Label,
            Level5Label = level5Label,
            Level6Label = level6Label,
            Level1Length = level1Length,
            Level2Length = level2Length,
            Level3Length = level3Length,
            Level4Length = level4Length,
            Level5Length = level5Length,
            Level6Length = level6Length,
            CreatedByUserId = auth.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            IsArchived = false,
            IsDefault = false
        };
        return await req.ToJsonAsync(created, HttpStatusCode.Created, jsonOptions);
    }

    [Function("Projects_SetDefault")]
    public async Task<HttpResponseData> SetDefaultAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/default")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");

        var updated = await memberRepository.SetDefaultProjectAsync(auth.UserId, projectId, req.FunctionContext.CancellationToken);
        if (!updated)
        {
            return await req.ErrorAsync(HttpStatusCode.NotFound, "Project not found or access denied.");
        }

        return await req.ToJsonAsync(new { projectId }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Projects_Update")]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");
        if (!auth.MfaEnabled) return await req.ErrorAsync(HttpStatusCode.Forbidden, "MFA required");

        var role = await memberRepository.GetRoleAsync(projectId, auth.UserId, req.FunctionContext.CancellationToken);
        if (role is null || Roles.Compare(role, Roles.Owner) < 0)
        {
            return await req.ErrorAsync(HttpStatusCode.Forbidden, "Owner role required");
        }

        UpdateProjectRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<UpdateProjectRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid update project payload.");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Name))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Name is required.");
        }

        await projectRepository.UpdateAsync(projectId, payload.Name.Trim(), payload.Description ?? string.Empty, req.FunctionContext.CancellationToken);
        var project = await projectRepository.GetAsync(projectId, auth.UserId, req.FunctionContext.CancellationToken);
        if (project is null)
        {
            return await req.ErrorAsync(HttpStatusCode.NotFound, "Project not found or access denied.");
        }

        return await req.ToJsonAsync(project, HttpStatusCode.OK, jsonOptions);
    }

    private sealed record CreateProjectRequest(
        string Name,
        string? Description,
        string? Separator,
        int PaddingLength,
        int LevelCount,
        string? Level1Label,
        string? Level2Label,
        string? Level3Label,
        string? Level4Label,
        string? Level5Label,
        string? Level6Label,
        int Level1Length,
        int Level2Length,
        int Level3Length,
        int Level4Length,
        int Level5Length,
        int Level6Length);

    private sealed record UpdateProjectRequest(string Name, string? Description);
}

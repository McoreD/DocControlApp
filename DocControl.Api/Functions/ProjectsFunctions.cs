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
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<ProjectsFunctions> logger;

    public ProjectsFunctions(AuthContextFactory authFactory, ProjectRepository projectRepository, IOptions<JsonSerializerOptions> jsonOptions, ILogger<ProjectsFunctions> logger)
    {
        this.authFactory = authFactory;
        this.projectRepository = projectRepository;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Projects_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects")] HttpRequestData req)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");

        var projects = await projectRepository.ListForUserAsync(auth.UserId);
        return await req.ToJsonAsync(projects, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Projects_Get")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");

        var project = await projectRepository.GetAsync(projectId, auth.UserId);
        if (project is null)
        {
            return req.Error(HttpStatusCode.NotFound, "Project not found or access denied.");
        }

        return await req.ToJsonAsync(project, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Projects_Create")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequestData req)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");

        CreateProjectRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<CreateProjectRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid create project payload.");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Name))
        {
            return req.Error(HttpStatusCode.BadRequest, "Name is required.");
        }

        var projectId = await projectRepository.CreateAsync(payload.Name.Trim(), payload.Description ?? string.Empty, auth.UserId, req.FunctionContext.CancellationToken);
        var created = new ProjectRecord
        {
            Id = projectId,
            Name = payload.Name.Trim(),
            Description = payload.Description ?? string.Empty,
            CreatedByUserId = auth.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            IsArchived = false
        };
        return await req.ToJsonAsync(created, HttpStatusCode.Created, jsonOptions);
    }

    private sealed record CreateProjectRequest(string Name, string? Description);
}

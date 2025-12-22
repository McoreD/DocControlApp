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

public sealed class ProjectMembersFunctions
{
    private readonly AuthContextFactory authFactory;
    private readonly ProjectRepository projectRepository;
    private readonly ProjectMemberRepository memberRepository;
    private readonly ProjectInviteRepository inviteRepository;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<ProjectMembersFunctions> logger;

    public ProjectMembersFunctions(
        AuthContextFactory authFactory,
        ProjectRepository projectRepository,
        ProjectMemberRepository memberRepository,
        ProjectInviteRepository inviteRepository,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<ProjectMembersFunctions> logger)
    {
        this.authFactory = authFactory;
        this.projectRepository = projectRepository;
        this.memberRepository = memberRepository;
        this.inviteRepository = inviteRepository;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("ProjectMembers_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/members")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, error) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");

        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken))
        {
            return req.Error(HttpStatusCode.Forbidden, "Access denied");
        }

        var members = await memberRepository.ListAsync(projectId, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(members, HttpStatusCode.OK, jsonOptions);
    }

    [Function("ProjectMembers_Invite")]
    public async Task<HttpResponseData> InviteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/invites")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, error) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Owner, req.FunctionContext.CancellationToken))
        {
            return req.Error(HttpStatusCode.Forbidden, "Owner role required");
        }

        InviteRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<InviteRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid invite payload");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
        {
            return req.Error(HttpStatusCode.BadRequest, "Email required");
        }

        var role = string.IsNullOrWhiteSpace(payload.Role) ? Roles.Viewer : payload.Role;
        var expires = DateTime.UtcNow.AddDays(payload.DaysValid ?? 7);
        var (token, inviteId) = await inviteRepository.CreateAsync(projectId, payload.Email.Trim(), role, auth.UserId, expires, req.FunctionContext.CancellationToken);

        // Return token (dev-time); in production send via email.
        return await req.ToJsonAsync(new { inviteId, token, expiresAtUtc = expires }, HttpStatusCode.Created, jsonOptions);
    }

    [Function("ProjectMembers_AcceptInvite")]
    public async Task<HttpResponseData> AcceptInviteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invites/accept")] HttpRequestData req)
    {
        var (ok, auth, error) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");

        AcceptInviteRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<AcceptInviteRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid accept payload");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
        {
            return req.Error(HttpStatusCode.BadRequest, "Token required");
        }

        var result = await inviteRepository.AcceptAsync(payload.Token.Trim(), auth.UserId, req.FunctionContext.CancellationToken);
        if (result is null)
        {
            return req.Error(HttpStatusCode.BadRequest, "Invalid or expired invite");
        }

        var (projectId, email, role) = result.Value;
        await memberRepository.AddOrUpdateMemberAsync(projectId, auth.UserId, role, auth.UserId, req.FunctionContext.CancellationToken);

        return await req.ToJsonAsync(new { projectId, role }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("ProjectMembers_Remove")]
    public async Task<HttpResponseData> RemoveAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projects/{projectId:long}/members/{userId:long}")] HttpRequestData req,
        long projectId,
        long userId)
    {
        var (ok, auth, error) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Owner, req.FunctionContext.CancellationToken))
        {
            return req.Error(HttpStatusCode.Forbidden, "Owner role required");
        }

        await memberRepository.RemoveAsync(projectId, userId, req.FunctionContext.CancellationToken);
        return req.ToJsonAsync(new { removed = userId }, HttpStatusCode.OK, jsonOptions).Result;
    }

    [Function("ProjectMembers_ChangeRole")]
    public async Task<HttpResponseData> ChangeRoleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/members/{userId:long}/role")] HttpRequestData req,
        long projectId,
        long userId)
    {
        var (ok, auth, error) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Owner, req.FunctionContext.CancellationToken))
        {
            return req.Error(HttpStatusCode.Forbidden, "Owner role required");
        }

        ChangeRoleRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<ChangeRoleRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid role payload");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Role))
        {
            return req.Error(HttpStatusCode.BadRequest, "Role required");
        }

        await memberRepository.AddOrUpdateMemberAsync(projectId, userId, payload.Role.Trim(), auth.UserId, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(new { userId, role = payload.Role.Trim() }, HttpStatusCode.OK, jsonOptions);
    }

    private async Task<bool> IsAtLeast(long projectId, long userId, string requiredRole, CancellationToken cancellationToken)
    {
        var role = await memberRepository.GetRoleAsync(projectId, userId, cancellationToken).ConfigureAwait(false);
        if (role is null) return false;
        return Roles.Compare(role, requiredRole) >= 0;
    }
}

public sealed record InviteRequest(string Email, string? Role, int? DaysValid);
public sealed record AcceptInviteRequest(string Token);
public sealed record ChangeRoleRequest(string Role);

using System.Net;
using System.Text.Json;
using System.Web;
using DocControl.Api.Infrastructure;
using DocControl.Core.Configuration;
using DocControl.Core.Models;
using DocControl.Infrastructure.Data;
using DocControl.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocControl.Api.Functions;

public sealed class DocumentsFunctions
{
    private readonly AuthContextFactory authFactory;
    private readonly ProjectMemberRepository memberRepository;
    private readonly DocumentRepository documentRepository;
    private readonly NumberAllocator allocator;
    private readonly AuditRepository auditRepository;
    private readonly ConfigService configService;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<DocumentsFunctions> logger;

    public DocumentsFunctions(
        AuthContextFactory authFactory,
        ProjectMemberRepository memberRepository,
        DocumentRepository documentRepository,
        NumberAllocator allocator,
        AuditRepository auditRepository,
        ConfigService configService,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<DocumentsFunctions> logger)
    {
        this.authFactory = authFactory;
        this.memberRepository = memberRepository;
        this.documentRepository = documentRepository;
        this.allocator = allocator;
        this.auditRepository = auditRepository;
        this.configService = configService;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Documents_List")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/documents")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Access denied");

        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var l1 = query["level1"];
        var l2 = query["level2"];
        var l3 = query["level3"];
        var filter = query["q"];

        var docs = await documentRepository.GetFilteredAsync(projectId, l1, l2, l3, filter, 200, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(docs, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Documents_Get")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/documents/{documentId:long}")] HttpRequestData req,
        long projectId,
        long documentId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Access denied");

        var doc = await documentRepository.GetByIdAsync(projectId, documentId, req.FunctionContext.CancellationToken);
        if (doc is null) return req.Error(HttpStatusCode.NotFound, "Not found");
        return await req.ToJsonAsync(doc, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Documents_Create")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:long}/documents")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Contributor, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Contributor role required");

        CreateDocumentRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<CreateDocumentRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid document payload");
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Level1) || string.IsNullOrWhiteSpace(payload.Level2) || string.IsNullOrWhiteSpace(payload.Level3))
        {
            return req.Error(HttpStatusCode.BadRequest, "Level1-3 required");
        }

        var config = await configService.LoadDocumentConfigAsync(projectId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        if (config.EnableLevel4 && string.IsNullOrWhiteSpace(payload.Level4))
        {
            return req.Error(HttpStatusCode.BadRequest, "Level4 is required for this project");
        }

        var key = new CodeSeriesKey
        {
            ProjectId = projectId,
            Level1 = payload.Level1.Trim(),
            Level2 = payload.Level2.Trim(),
            Level3 = payload.Level3.Trim(),
            Level4 = string.IsNullOrWhiteSpace(payload.Level4) ? null : payload.Level4.Trim()
        };

        var allocated = await allocator.AllocateAsync(key, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        var generator = new CodeGenerator(config);
        var fileName = generator.BuildFileName(key, allocated.Number, payload.FreeText ?? string.Empty, payload.Extension);
        var now = DateTime.UtcNow;

        var docId = await documentRepository.InsertAsync(allocated, key, payload.FreeText ?? string.Empty, fileName, auth.UserId, now, payload.OriginalQuery, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        await auditRepository.InsertAsync(projectId, "DocumentCreated", fileName, auth.UserId, now, docId, req.FunctionContext.CancellationToken).ConfigureAwait(false);

        return await req.ToJsonAsync(new { id = docId, number = allocated.Number, fileName }, HttpStatusCode.Created, jsonOptions);
    }

    [Function("Audit_List")]
    public async Task<HttpResponseData> AuditListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:long}/audit")] HttpRequestData req,
        long projectId)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return req.Error(HttpStatusCode.Unauthorized, "Auth required");
        if (!await IsAtLeast(projectId, auth.UserId, Roles.Viewer, req.FunctionContext.CancellationToken)) return req.Error(HttpStatusCode.Forbidden, "Access denied");

        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var action = query["action"];
        var user = query["user"];
        var take = int.TryParse(query["take"], out var t) ? t : 50;
        var skip = int.TryParse(query["skip"], out var s) ? s : 0;

        var entries = await auditRepository.GetPagedAsync(projectId, take, skip, action, user, req.FunctionContext.CancellationToken);
        return await req.ToJsonAsync(entries, HttpStatusCode.OK, jsonOptions);
    }

    private async Task<bool> IsAtLeast(long projectId, long userId, string requiredRole, CancellationToken cancellationToken)
    {
        var role = await memberRepository.GetRoleAsync(projectId, userId, cancellationToken).ConfigureAwait(false);
        if (role is null) return false;
        return Roles.Compare(role, requiredRole) >= 0;
    }
}

public sealed record CreateDocumentRequest(string Level1, string Level2, string Level3, string? Level4, string? FreeText, string? Extension, string? OriginalQuery);

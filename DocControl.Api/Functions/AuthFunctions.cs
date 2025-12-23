using System.Net;
using System.Text.Json;
using DocControl.Api.Infrastructure;
using DocControl.Api.Services;
using DocControl.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocControl.Api.Functions;

public sealed class AuthFunctions
{
    private readonly UserRepository userRepository;
    private readonly UserAuthRepository userAuthRepository;
    private readonly AuthContextFactory authFactory;
    private readonly MfaService mfaService;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<AuthFunctions> logger;

    public AuthFunctions(
        UserRepository userRepository,
        UserAuthRepository userAuthRepository,
        AuthContextFactory authFactory,
        MfaService mfaService,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<AuthFunctions> logger)
    {
        this.userRepository = userRepository;
        this.userAuthRepository = userAuthRepository;
        this.authFactory = authFactory;
        this.mfaService = mfaService;
        this.jsonOptions = jsonOptions.Value;
        this.logger = logger;
    }

    [Function("Auth_Register")]
    public async Task<HttpResponseData> RegisterAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/register")] HttpRequestData req)
    {
        RegisterRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<RegisterRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid register payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Email is required");
        }

        var displayName = string.IsNullOrWhiteSpace(payload.DisplayName) ? payload.Email.Trim() : payload.DisplayName.Trim();
        var user = await userRepository.RegisterAsync(payload.Email.Trim(), displayName, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        await userAuthRepository.EnsureExistsAsync(user.Id, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        return await req.ToJsonAsync(new { user.Id, user.Email, user.DisplayName, user.CreatedAtUtc, MfaEnabled = false }, HttpStatusCode.Created, jsonOptions);
    }

    [Function("Auth_Me")]
    public async Task<HttpResponseData> MeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/me")] HttpRequestData req)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");

        var authRecord = await userAuthRepository.GetAsync(auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        return await req.ToJsonAsync(new { auth.UserId, Email = auth.Email, DisplayName = auth.DisplayName, MfaEnabled = authRecord?.MfaEnabled ?? false }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Auth_Mfa_Start")]
    public async Task<HttpResponseData> StartMfaAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/mfa/start")] HttpRequestData req)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");

        var secret = mfaService.GenerateSecret();
        await userAuthRepository.SaveTotpAsync(auth.UserId, secret, verified: false, req.FunctionContext.CancellationToken).ConfigureAwait(false);

        var uri = mfaService.BuildOtpAuthUri(secret, auth.Email);
        return await req.ToJsonAsync(new { secret, otpauthUrl = uri }, HttpStatusCode.OK, jsonOptions);
    }

    [Function("Auth_Mfa_Verify")]
    public async Task<HttpResponseData> VerifyMfaAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/mfa/verify")] HttpRequestData req)
    {
        var (ok, auth, _) = await authFactory.BindAsync(req, req.FunctionContext.CancellationToken);
        if (!ok || auth is null) return await req.ErrorAsync(HttpStatusCode.Unauthorized, "Auth required");

        VerifyMfaRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<VerifyMfaRequest>(req.Body, jsonOptions, req.FunctionContext.CancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid verify payload");
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Code))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Code required");
        }

        var authRecord = await userAuthRepository.GetAsync(auth.UserId, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        if (authRecord?.MfaMethodsJson is null)
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Start MFA setup first");
        }

        TotpState? state;
        try
        {
            state = JsonSerializer.Deserialize<TotpState>(authRecord.MfaMethodsJson);
        }
        catch (JsonException)
        {
            state = null;
        }

        if (state is null || string.IsNullOrWhiteSpace(state.Secret))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "MFA secret missing");
        }

        if (!mfaService.VerifyCode(state.Secret, payload.Code))
        {
            return await req.ErrorAsync(HttpStatusCode.BadRequest, "Invalid code");
        }

        await userAuthRepository.SaveTotpAsync(auth.UserId, state.Secret, verified: true, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        return await req.ToJsonAsync(new { mfaEnabled = true }, HttpStatusCode.OK, jsonOptions);
    }
}

internal sealed record RegisterRequest(string Email, string? DisplayName);
internal sealed record VerifyMfaRequest(string Code);
internal sealed record TotpState(string Secret, DateTime CreatedAtUtc, DateTime? VerifiedAtUtc);

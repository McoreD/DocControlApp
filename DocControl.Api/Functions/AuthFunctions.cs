using System.Net;
using System.Text.Json;
using DocControl.Api.Infrastructure;
using DocControl.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocControl.Api.Functions;

public sealed class AuthFunctions
{
    private readonly UserRepository userRepository;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILogger<AuthFunctions> logger;

    public AuthFunctions(UserRepository userRepository, IOptions<JsonSerializerOptions> jsonOptions, ILogger<AuthFunctions> logger)
    {
        this.userRepository = userRepository;
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
            return req.Error(HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
        {
            return req.Error(HttpStatusCode.BadRequest, "Email is required");
        }

        var displayName = string.IsNullOrWhiteSpace(payload.DisplayName) ? payload.Email.Trim() : payload.DisplayName.Trim();
        var user = await userRepository.RegisterAsync(payload.Email.Trim(), displayName, req.FunctionContext.CancellationToken).ConfigureAwait(false);
        return await req.ToJsonAsync(user, HttpStatusCode.Created, jsonOptions);
    }
}

internal sealed record RegisterRequest(string Email, string? DisplayName);

using DocControl.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker.Http;

namespace DocControl.Api.Infrastructure;

public sealed record AuthContext(long UserId, string Email, string DisplayName);

public sealed class AuthContextFactory
{
    private readonly UserRepository userRepository;

    public AuthContextFactory(UserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    public async Task<(bool ok, AuthContext? context, HttpResponseData? error)> BindAsync(HttpRequestData req, CancellationToken cancellationToken = default)
    {
        if (!req.Headers.TryGetValues("x-user-id", out var userIds))
        {
            return (false, null, req.CreateResponse(System.Net.HttpStatusCode.Unauthorized));
        }

        var rawId = userIds.FirstOrDefault();
        if (!long.TryParse(rawId, out var userId) || userId <= 0)
        {
            return (false, null, req.CreateResponse(System.Net.HttpStatusCode.Unauthorized));
        }

        var email = GetHeader(req, "x-user-email") ?? $"user{userId}@example.com";
        var name = GetHeader(req, "x-user-name") ?? email;

        var ensuredId = await userRepository.GetOrCreateAsync(email, name, cancellationToken).ConfigureAwait(false);
        if (ensuredId != userId)
        {
            userId = ensuredId;
        }

        return (true, new AuthContext(userId, email, name), null);
    }

    private static string? GetHeader(HttpRequestData req, string name) =>
        req.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}

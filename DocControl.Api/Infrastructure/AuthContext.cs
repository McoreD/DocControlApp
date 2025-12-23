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
            return (false, null, req.Error(System.Net.HttpStatusCode.Unauthorized, "Missing user id header"));
        }

        var rawId = userIds.FirstOrDefault();
        if (!long.TryParse(rawId, out var userId) || userId <= 0)
        {
            return (false, null, req.Error(System.Net.HttpStatusCode.Unauthorized, "Invalid user id"));
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return (false, null, req.Error(System.Net.HttpStatusCode.Unauthorized, "User not registered"));
        }

        return (true, new AuthContext(user.Id, user.Email, user.DisplayName), null);
    }
}

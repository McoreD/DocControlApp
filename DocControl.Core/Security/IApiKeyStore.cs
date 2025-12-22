namespace DocControl.Core.Security;

public interface IApiKeyStore
{
    Task SaveAsync(string name, string apiKey, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string name, CancellationToken cancellationToken = default);
}

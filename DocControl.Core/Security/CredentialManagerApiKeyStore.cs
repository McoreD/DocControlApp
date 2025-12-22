using CredentialManagement;

namespace DocControl.Core.Security;

public sealed class CredentialManagerApiKeyStore : IApiKeyStore
{
    public Task SaveAsync(string name, string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Credential name is required", nameof(name));
        using var cred = new Credential
        {
            Target = name,
            Username = "apikey",
            Password = apiKey,
            PersistanceType = PersistanceType.LocalComputer,
            Type = CredentialType.Generic
        };
        cred.Save();
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Credential name is required", nameof(name));
        using var cred = new Credential { Target = name, Type = CredentialType.Generic };
        var loaded = cred.Load();
        return Task.FromResult(loaded ? cred.Password : null);
    }
}

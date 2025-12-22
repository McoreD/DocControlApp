using System.Text.Json;
using DocControl.AI;
using DocControl.Core.Configuration;
using DocControl.Infrastructure.Data;
using DocControl.Core.Security;

namespace DocControl.Infrastructure.Services;

public sealed class ConfigService
{
    private const string DocumentConfigKey = "DocumentConfig";
    private const string AiSettingsKey = "AiSettings";
    private const string ProjectScope = "Project";

    private readonly ConfigRepository configRepository;
    private readonly IApiKeyStore apiKeyStore;

    public ConfigService(ConfigRepository configRepository, IApiKeyStore apiKeyStore)
    {
        this.configRepository = configRepository;
        this.apiKeyStore = apiKeyStore;
    }

    public async Task<DocumentConfig> LoadDocumentConfigAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var json = await configRepository.GetAsync(ProjectScope, projectId, DocumentConfigKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return new DocumentConfig();
        try
        {
            return JsonSerializer.Deserialize<DocumentConfig>(json) ?? new DocumentConfig();
        }
        catch
        {
            return new DocumentConfig();
        }
    }

    public async Task SaveDocumentConfigAsync(long projectId, DocumentConfig config, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(config);
        await configRepository.SetAsync(ProjectScope, projectId, DocumentConfigKey, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSettings> LoadAiSettingsAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var json = await configRepository.GetAsync(ProjectScope, projectId, AiSettingsKey, cancellationToken).ConfigureAwait(false);
        AiSettings settings;
        if (string.IsNullOrWhiteSpace(json))
        {
            settings = new AiSettings();
        }
        else
        {
            try
            {
                settings = JsonSerializer.Deserialize<AiSettings>(json) ?? new AiSettings();
            }
            catch
            {
                settings = new AiSettings();
            }
        }

        // Load keys from secure store (not returned for safety)
        await apiKeyStore.GetAsync(settings.OpenAiCredentialName, cancellationToken).ConfigureAwait(false);
        await apiKeyStore.GetAsync(settings.GeminiCredentialName, cancellationToken).ConfigureAwait(false);
        return settings;
    }

    public async Task SaveAiSettingsAsync(long projectId, AiSettings settings, string openAiKey, string geminiKey, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings);
        await configRepository.SetAsync(ProjectScope, projectId, AiSettingsKey, json, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            await apiKeyStore.SaveAsync(settings.OpenAiCredentialName, openAiKey, cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            await apiKeyStore.SaveAsync(settings.GeminiCredentialName, geminiKey, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<AiClientOptions> BuildAiOptionsAsync(AiSettings settings, CancellationToken cancellationToken = default)
    {
        return AiOptionsBuilder.BuildAsync(settings, apiKeyStore, cancellationToken);
    }
}

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
    private readonly UserRepository userRepository;

    public ConfigService(
        ConfigRepository configRepository,
        UserRepository userRepository)
    {
        this.configRepository = configRepository;
        this.userRepository = userRepository;
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

        return settings;
    }

    public async Task<(bool hasOpenAi, bool hasGemini)> GetAiKeyStatusAsync(AiSettings settings, long userId, CancellationToken cancellationToken = default)
    {
        var (openAiEncrypted, geminiEncrypted) = await userRepository.GetAiKeysEncryptedAsync(userId, cancellationToken).ConfigureAwait(false);
        return (!string.IsNullOrWhiteSpace(openAiEncrypted), !string.IsNullOrWhiteSpace(geminiEncrypted));
    }

    public async Task SaveAiSettingsAsync(
        long projectId,
        long userId,
        AiSettings settings,
        string openAiKey,
        string geminiKey,
        bool clearOpenAi,
        bool clearGemini,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings);
        await configRepository.SetAsync(ProjectScope, projectId, AiSettingsKey, json, cancellationToken).ConfigureAwait(false);

        var user = await userRepository.GetPasswordAuthByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user?.PasswordHash is null || user.KeySalt is null)
        {
            throw new InvalidOperationException("Password is required to store API keys.");
        }

        var protector = CreateProtector(user.PasswordHash, user.KeySalt);
        var openAiEncrypted = string.IsNullOrWhiteSpace(openAiKey) ? null : protector.Encrypt(openAiKey);
        var geminiEncrypted = string.IsNullOrWhiteSpace(geminiKey) ? null : protector.Encrypt(geminiKey);
        if (openAiEncrypted is not null || geminiEncrypted is not null || clearOpenAi || clearGemini)
        {
            await userRepository.SaveAiKeysEncryptedAsync(userId, openAiEncrypted, geminiEncrypted, clearOpenAi, clearGemini, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<AiClientOptions> BuildAiOptionsAsync(AiSettings settings, long userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var user = await userRepository.GetPasswordAuthByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user?.PasswordHash is null || user.KeySalt is null)
        {
            return new AiClientOptions
            {
                DefaultProvider = settings.Provider,
                OpenAi = new OpenAiOptions { ApiKey = string.Empty, Model = settings.OpenAiModel },
                Gemini = new GeminiOptions { ApiKey = string.Empty, Model = settings.GeminiModel }
            };
        }

        var protector = CreateProtector(user.PasswordHash, user.KeySalt);
        var (openAiEncrypted, geminiEncrypted) = await userRepository.GetAiKeysEncryptedAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var openAiKey = protector.Decrypt(openAiEncrypted ?? string.Empty) ?? string.Empty;
        var geminiKey = protector.Decrypt(geminiEncrypted ?? string.Empty) ?? string.Empty;

        return new AiClientOptions
        {
            DefaultProvider = settings.Provider,
            OpenAi = new OpenAiOptions { ApiKey = openAiKey, Model = settings.OpenAiModel },
            Gemini = new GeminiOptions { ApiKey = geminiKey, Model = settings.GeminiModel }
        };
    }

    private static AesGcmSecretProtector CreateProtector(string passwordHash, string keySalt)
    {
        var key = PasswordHasher.DeriveEncryptionKey(passwordHash, keySalt);
        return new AesGcmSecretProtector(key);
    }
}

using System.Text.Json;
using DocControl.AI;
using DocControl.Core.Configuration;
using DocControl.Infrastructure.Data;
using DocControl.Core.Security;

namespace DocControl.Infrastructure.Services;

public sealed class ConfigService
{
    private const string AiSettingsKey = "AiSettings";
    private const string ProjectScope = "Project";
    private readonly ConfigRepository configRepository;
    private readonly UserRepository userRepository;
    private readonly ProjectRepository projectRepository;

    public ConfigService(
        ConfigRepository configRepository,
        UserRepository userRepository,
        ProjectRepository projectRepository)
    {
        this.configRepository = configRepository;
        this.userRepository = userRepository;
        this.projectRepository = projectRepository;
    }

    public async Task<DocumentConfig> LoadDocumentConfigAsync(long projectId, long userId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetAsync(projectId, userId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return new DocumentConfig();
        }
        return new DocumentConfig
        {
            Separator = string.IsNullOrWhiteSpace(project.Separator) ? "-" : project.Separator,
            PaddingLength = project.PaddingLength <= 0 ? 3 : project.PaddingLength,
            LevelCount = project.LevelCount is < 1 or > 6 ? 3 : project.LevelCount,
            Level1Name = string.IsNullOrWhiteSpace(project.Level1Label) ? "Level1" : project.Level1Label,
            Level2Name = string.IsNullOrWhiteSpace(project.Level2Label) ? "Level2" : project.Level2Label,
            Level3Name = string.IsNullOrWhiteSpace(project.Level3Label) ? "Level3" : project.Level3Label,
            Level4Name = string.IsNullOrWhiteSpace(project.Level4Label) ? "Level4" : project.Level4Label,
            Level5Name = string.IsNullOrWhiteSpace(project.Level5Label) ? "Level5" : project.Level5Label,
            Level6Name = string.IsNullOrWhiteSpace(project.Level6Label) ? "Level6" : project.Level6Label,
            Level1Length = project.Level1Length is < 1 or > 4 ? 3 : project.Level1Length,
            Level2Length = project.Level2Length is < 1 or > 4 ? 3 : project.Level2Length,
            Level3Length = project.Level3Length is < 1 or > 4 ? 3 : project.Level3Length,
            Level4Length = project.Level4Length is < 1 or > 4 ? 3 : project.Level4Length,
            Level5Length = project.Level5Length is < 1 or > 4 ? 3 : project.Level5Length,
            Level6Length = project.Level6Length is < 1 or > 4 ? 3 : project.Level6Length
        };
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

    public async Task<(string? openAiSuffix, string? geminiSuffix)> GetAiKeySuffixesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetPasswordAuthByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user?.PasswordHash is null || user.KeySalt is null)
        {
            return (null, null);
        }

        var protector = CreateProtector(user.PasswordHash, user.KeySalt);
        var (openAiEncrypted, geminiEncrypted) = await userRepository.GetAiKeysEncryptedAsync(userId, cancellationToken).ConfigureAwait(false);
        var openAiKey = string.IsNullOrWhiteSpace(openAiEncrypted) ? null : protector.Decrypt(openAiEncrypted);
        var geminiKey = string.IsNullOrWhiteSpace(geminiEncrypted) ? null : protector.Decrypt(geminiEncrypted);
        return (GetSuffix(openAiKey), GetSuffix(geminiKey));
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

    private static string? GetSuffix(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var trimmed = key.Trim();
        return trimmed.Length <= 3 ? trimmed : trimmed[^3..];
    }
}

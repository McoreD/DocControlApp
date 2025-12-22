using DocControl.AI;
using DocControl.Core.Security;

namespace DocControl.Core.Configuration;

public static class AiOptionsBuilder
{
    public static async Task<AiClientOptions> BuildAsync(AiSettings settings, IApiKeyStore keyStore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keyStore);

        var openAiKey = await keyStore.GetAsync(settings.OpenAiCredentialName, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        var geminiKey = await keyStore.GetAsync(settings.GeminiCredentialName, cancellationToken).ConfigureAwait(false) ?? string.Empty;

        return new AiClientOptions
        {
            DefaultProvider = settings.Provider,
            OpenAi = new OpenAiOptions
            {
                ApiKey = openAiKey,
                Model = settings.OpenAiModel
            },
            Gemini = new GeminiOptions
            {
                ApiKey = geminiKey,
                Model = settings.GeminiModel
            }
        };
    }
}

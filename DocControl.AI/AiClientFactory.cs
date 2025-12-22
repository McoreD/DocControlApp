namespace DocControl.AI;

public enum AiProvider
{
    OpenAi,
    Gemini
}

public sealed class AiClientFactory
{
    private readonly OpenAiClient openAiClient;
    private readonly GeminiClient geminiClient;

    public AiClientFactory(OpenAiClient openAiClient, GeminiClient geminiClient)
    {
        this.openAiClient = openAiClient;
        this.geminiClient = geminiClient;
    }

    public IAiClient GetClient(AiProvider provider) => provider switch
    {
        AiProvider.OpenAi => openAiClient,
        AiProvider.Gemini => geminiClient,
        _ => openAiClient
    };
}

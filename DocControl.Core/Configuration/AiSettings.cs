using DocControl.AI;

namespace DocControl.Core.Configuration;

public sealed class AiSettings
{
    public AiProvider Provider { get; set; } = AiProvider.OpenAi;
    public string OpenAiModel { get; set; } = "gpt-4.1";
    public string GeminiModel { get; set; } = "gemini-3-flash-preview";
    public string OpenAiCredentialName { get; set; } = "DocControl:OpenAI";
    public string GeminiCredentialName { get; set; } = "DocControl:Gemini";
}

using DocControl.AI;
using DocControl.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocControl.Api.Infrastructure;

public sealed class AiOrchestratorFactory
{
    private readonly ConfigService configService;
    private readonly IHttpClientFactory httpClientFactory;

    public AiOrchestratorFactory(ConfigService configService, IHttpClientFactory httpClientFactory)
    {
        this.configService = configService;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<AiOrchestrator?> CreateAsync(long projectId, long userId, CancellationToken cancellationToken = default)
    {
        var settings = await configService.LoadAiSettingsAsync(projectId, cancellationToken).ConfigureAwait(false);
        var options = await configService.BuildAiOptionsAsync(settings, userId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(options.OpenAi.ApiKey) && string.IsNullOrWhiteSpace(options.Gemini.ApiKey))
        {
            return null;
        }

        var openAiClient = new OpenAiClient(httpClientFactory.CreateClient(nameof(OpenAiClient)), options.OpenAi);
        var geminiClient = new GeminiClient(httpClientFactory.CreateClient(nameof(GeminiClient)), options.Gemini);
        var factory = new AiClientFactory(openAiClient, geminiClient);
        return new AiOrchestrator(factory, options);
    }
}

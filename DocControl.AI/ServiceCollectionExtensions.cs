using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DocControl.AI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiClients(this IServiceCollection services, AiClientOptions options)
    {
        services.AddSingleton(options);

        services.AddHttpClient(nameof(OpenAiClient));
        services.AddSingleton(provider => new OpenAiClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenAiClient)),
            options.OpenAi));

        services.AddHttpClient(nameof(GeminiClient));
        services.AddSingleton(provider => new GeminiClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GeminiClient)),
            options.Gemini));

        services.AddSingleton<AiClientFactory>();
        services.AddSingleton<AiOrchestrator>();
        return services;
    }
}

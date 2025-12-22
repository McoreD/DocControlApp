namespace DocControl.AI;

public sealed class AiOrchestrator
{
    private readonly AiClientFactory clientFactory;
    private readonly AiClientOptions options;

    public AiOrchestrator(AiClientFactory clientFactory, AiClientOptions options)
    {
        this.clientFactory = clientFactory;
        this.options = options;
    }

    public Task<AiStructuredResult> ExecuteAsync(AiStructuredRequest request, AiProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var selected = provider ?? options.DefaultProvider;
        var client = clientFactory.GetClient(selected);
        return client.GetStructuredCompletionAsync(request, cancellationToken);
    }
}

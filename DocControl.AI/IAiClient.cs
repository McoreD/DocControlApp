namespace DocControl.AI;

public interface IAiClient
{
    Task<AiStructuredResult> GetStructuredCompletionAsync(AiStructuredRequest request, CancellationToken cancellationToken = default);
}

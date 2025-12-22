using DocControl.Infrastructure.Data;
using DocControl.Core.Models;

namespace DocControl.Infrastructure.Services;

public sealed class RecommendationService
{
    private readonly FileNameParser parser;
    private readonly CodeSeriesRepository seriesRepo;
    private readonly DocumentRepository documentRepo;

    public RecommendationService(FileNameParser parser, CodeSeriesRepository seriesRepo, DocumentRepository documentRepo)
    {
        this.parser = parser;
        this.seriesRepo = seriesRepo;
        this.documentRepo = documentRepo;
    }

    public async Task<RecommendationResult> RecommendAsync(CodeSeriesKey requestedKey, CancellationToken cancellationToken = default)
    {
        var max = await documentRepo.GetMaxNumberAsync(requestedKey, cancellationToken).ConfigureAwait(false);
        var exists = max.HasValue;
        var suggestedNext = (max ?? 0) + 1;
        return new RecommendationResult(requestedKey, exists, suggestedNext, max ?? 0);
    }
}

public sealed record RecommendationResult(CodeSeriesKey SeriesKey, bool IsExisting, int SuggestedNext, int ExistingMax);

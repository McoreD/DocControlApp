namespace DocControl.Core.Models;

public sealed class ImportSeriesSummary
{
    public required CodeSeriesKey SeriesKey { get; init; }
    public int MaxNumber { get; init; }
    public int NextNumber { get; init; }
}

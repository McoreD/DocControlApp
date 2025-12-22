namespace DocControl.Core.Models;

public sealed record ParsedFileName(CodeSeriesKey SeriesKey, int Number, string FreeText, string? Extension);

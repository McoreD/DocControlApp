using System.Text.Json;

namespace DocControl.AI;

public sealed class AiStructuredResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public string? RawResponse { get; init; }
    public JsonElement? ParsedPayload { get; init; }

    public static AiStructuredResult Success(string raw, JsonElement parsed) => new()
    {
        IsSuccess = true,
        RawResponse = raw,
        ParsedPayload = parsed
    };

    public static AiStructuredResult Failure(string error, string? raw = null) => new()
    {
        IsSuccess = false,
        Error = error,
        RawResponse = raw
    };
}

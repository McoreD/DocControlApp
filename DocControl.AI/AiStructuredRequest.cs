using System.Text.Json;

namespace DocControl.AI;

public sealed class AiStructuredRequest
{
    public required string Prompt { get; init; }
    public required JsonElement ResponseSchema { get; init; }
    public string? SystemInstruction { get; init; }
}

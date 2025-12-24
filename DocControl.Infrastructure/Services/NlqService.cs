using System.Text.Json;
using System.Linq;
using DocControl.AI;
using DocControl.Core.Models;

namespace DocControl.Infrastructure.Services;

public sealed class NlqService
{
    private readonly AiOrchestrator orchestrator;

    public NlqService(AiOrchestrator orchestrator)
    {
        this.orchestrator = orchestrator;
    }

    public async Task<NlqResponse?> InterpretAsync(string query, CancellationToken cancellationToken = default)
    {
        var schema = JsonDocument.Parse("""
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "documentType": { "type": "string" },
    "owner": { "type": "string" },
    "level1": { "type": "string" },
    "level2": { "type": "string" },
    "level3": { "type": "string" },
    "level4": { "type": "string" },
    "freeText": { "type": "string" }
  },
  "required": ["documentType", "owner", "level1", "level2", "level3", "level4", "freeText"]
}
""").RootElement.Clone();

        var request = new AiStructuredRequest
        {
            Prompt = query,
            ResponseSchema = schema,
            SystemInstruction = "Return a strict JSON object with documentType, owner, level1, level2, level3, optional level4, and freeText. Do not return natural language."
        };

        var result = await orchestrator.ExecuteAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error ?? "AI call failed")
            {
                Data = { ["raw"] = result.RawResponse ?? string.Empty }
            };
        }

        if (result.ParsedPayload is null)
        {
            throw new InvalidOperationException("AI returned empty payload")
            {
                Data = { ["raw"] = result.RawResponse ?? string.Empty }
            };
        }

        try
        {
            var payload = result.ParsedPayload.Value;
            return new NlqResponse
            {
                DocumentType = payload.TryGetProperty("documentType", out var dt) ? dt.GetString() : null,
                Owner = payload.TryGetProperty("owner", out var ow) ? ow.GetString() : null,
                Level1 = payload.GetProperty("level1").GetString() ?? string.Empty,
                Level2 = payload.GetProperty("level2").GetString() ?? string.Empty,
                Level3 = payload.GetProperty("level3").GetString() ?? string.Empty,
                Level4 = payload.TryGetProperty("level4", out var l4) ? l4.GetString() : null,
                FreeText = payload.GetProperty("freeText").GetString() ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse AI response", ex)
            {
                Data = { ["raw"] = result.RawResponse ?? string.Empty }
            };
        }
    }

    public async Task<NlqResponse?> RecommendCodeAsync(string query, IReadOnlyList<(int Level, string Code, String Description)> codes, CancellationToken cancellationToken = default)
    {
        var catalog = codes.Select(c => new { c.Level, c.Code, c.Description }).ToList();
        var catalogJson = JsonSerializer.Serialize(catalog);

        var schema = JsonDocument.Parse("""
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "level1": { "type": "string" },
    "level2": { "type": "string" },
    "level3": { "type": "string" },
    "level4": { "type": "string" },
    "freeText": { "type": "string" },
    "reason": { "type": "string" }
  },
  "required": ["level1", "level2", "level3", "level4", "freeText", "reason"]
}
""").RootElement.Clone();

        var request = new AiStructuredRequest
        {
            Prompt = $@"You are a strict code recommender. Given the user request and the catalog, choose exactly one best code per level (1-3) from the catalog if available; do not concatenate multiple codes into a single field. If no suitable code exists for a level, invent a concise alphanumeric code and mark the reason.

User query: {query}

Catalog (array of objects): {catalogJson}

Rules:
- level1 must be a single level-1 code (no separators). Pick the best match; do not combine multiple codes.
- level2 must be a single level-2 code (no separators). Pick the best match; do not combine multiple codes.
- level3 must be a single level-3 code (no separators). Pick the best match; do not combine multiple codes.
- level4 is optional; leave empty/null unless clearly required.
- freeText should be a short human-readable description of the document/request.
- Reason should briefly explain why you chose these codes.

Return JSON only.",
            ResponseSchema = schema,
            SystemInstruction = "Return a strict JSON object with level1, level2, level3, optional level4, freeText, and a brief reason. Use existing codes when possible. Never concatenate multiple codes into one field."
        };

        var result = await orchestrator.ExecuteAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error ?? "AI call failed") { Data = { ["raw"] = result.RawResponse ?? string.Empty } };
        }

        if (result.ParsedPayload is null)
        {
            throw new InvalidOperationException("AI returned empty payload") { Data = { ["raw"] = result.RawResponse ?? string.Empty } };
        }

        try
        {
            var payload = result.ParsedPayload.Value;
            return new NlqResponse
            {
                Level1 = payload.GetProperty("level1").GetString() ?? string.Empty,
                Level2 = payload.GetProperty("level2").GetString() ?? string.Empty,
                Level3 = payload.GetProperty("level3").GetString() ?? string.Empty,
                Level4 = payload.TryGetProperty("level4", out var l4) ? l4.GetString() : null,
                FreeText = payload.GetProperty("freeText").GetString() ?? string.Empty,
                DocumentType = null,
                Owner = null
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse AI response", ex) { Data = { ["raw"] = result.RawResponse ?? string.Empty } };
        }
    }
}

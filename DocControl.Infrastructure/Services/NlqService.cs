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
    "level5": { "type": "string" },
    "level6": { "type": "string" },
    "freeText": { "type": "string" }
  },
  "required": ["documentType", "owner", "level1", "level2", "level3", "freeText"]
}
""").RootElement.Clone();

        var request = new AiStructuredRequest
        {
            Prompt = query,
            ResponseSchema = schema,
            SystemInstruction = "Return a strict JSON object with documentType, owner, level1, level2, level3, optional level4-6, and freeText. Do not return natural language."
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
                Level5 = payload.TryGetProperty("level5", out var l5) ? l5.GetString() : null,
                Level6 = payload.TryGetProperty("level6", out var l6) ? l6.GetString() : null,
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
        var level1 = codes.Where(c => c.Level == 1).Select(c => new { c.Code, c.Description }).DistinctBy(c => c.Code).ToList();
        var level2 = codes.Where(c => c.Level == 2).Select(c => new { c.Code, c.Description }).DistinctBy(c => c.Code).ToList();
        var level3 = codes.Where(c => c.Level == 3).Select(c => new { c.Code, c.Description }).DistinctBy(c => c.Code).ToList();
        var level4 = codes.Where(c => c.Level == 4).Select(c => new { c.Code, c.Description }).DistinctBy(c => c.Code).ToList();
        var level5 = codes.Where(c => c.Level == 5).Select(c => new { c.Code, c.Description }).DistinctBy(c => c.Code).ToList();
        var level6 = codes.Where(c => c.Level == 6).Select(c => new { c.Code, c.Description }).DistinctBy(c => c.Code).ToList();
        var catalogJson = JsonSerializer.Serialize(new
        {
            level1,
            level2,
            level3,
            level4,
            level5,
            level6
        });

        var schema = JsonDocument.Parse("""
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "level1": { "type": "string" },
    "level2": { "type": "string" },
    "level3": { "type": "string" },
    "level4": { "type": "string" },
    "level5": { "type": "string" },
    "level6": { "type": "string" },
    "freeText": { "type": "string" },
    "reason": { "type": "string" }
  },
  "required": ["level1", "level2", "level3", "freeText", "reason"]
}
""").RootElement.Clone();

        var request = new AiStructuredRequest
        {
            Prompt = $@"You are a strict code recommender. Given the user request and the catalog lists for each level, choose exactly one best code per level (1-3) from the matching list; never concatenate multiple codes. If no suitable code exists for a level, invent a concise alphanumeric code and note why.

User query: {query}

Catalog (separate arrays per level):
{catalogJson}

Rules:
- level1 must be a single value from level1 list (no separators). If none fit, invent a short code.
- level2 must be a single value from level2 list. Do not prepend/append other codes.
- level3 must be a single value from level3 list. Do not prepend/append other codes.
- level4-level6 are optional; leave empty/null unless clearly required.
- freeText should be a short human-readable description of the document/request.
- Reason should briefly explain why you chose these codes.

Return JSON only.",
            ResponseSchema = schema,
            SystemInstruction = "Return a strict JSON object with level1, level2, level3, optional level4-6, freeText, and a brief reason. Use existing codes from their respective lists when possible. Never concatenate multiple codes into one field."
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
                Level5 = payload.TryGetProperty("level5", out var l5) ? l5.GetString() : null,
                Level6 = payload.TryGetProperty("level6", out var l6) ? l6.GetString() : null,
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

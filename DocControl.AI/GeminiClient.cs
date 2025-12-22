using System.Net.Http.Json;
using System.Text.Json;

namespace DocControl.AI;

public sealed class GeminiClient : IAiClient
{
    private readonly HttpClient httpClient;
    private readonly GeminiOptions options;

    public GeminiClient(HttpClient httpClient, GeminiOptions options)
    {
        this.httpClient = httpClient;
        this.options = options;
    }

    public async Task<AiStructuredResult> GetStructuredCompletionAsync(AiStructuredRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = new Uri($"{options.Endpoint}{options.Model}:generateContent?key={options.ApiKey}");
        var prompt = request.SystemInstruction is null ? request.Prompt : $"{request.SystemInstruction}\n\n{request.Prompt}";
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        using var response = await httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return AiStructuredResult.Failure($"Gemini error {(int)response.StatusCode}: {response.ReasonPhrase}", raw);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0)
            {
                var content = candidates[0].GetProperty("content");
                if (content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        try
                        {
                            using var parsedDoc = JsonDocument.Parse(text);
                            return AiStructuredResult.Success(raw, parsedDoc.RootElement.Clone());
                        }
                        catch (JsonException)
                        {
                            return AiStructuredResult.Failure("Gemini response text was not valid JSON.", raw);
                        }
                    }
                }

                return AiStructuredResult.Failure("Gemini response missing text content.", raw);
            }

            return AiStructuredResult.Failure("Gemini response missing 'candidates' payload.", raw);
        }
        catch (JsonException)
        {
            return AiStructuredResult.Failure("Failed to parse Gemini response as JSON.", raw);
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DocControl.AI;

public sealed class OpenAiClient : IAiClient
{
    private readonly HttpClient httpClient;
    private readonly OpenAiOptions options;

    public OpenAiClient(HttpClient httpClient, OpenAiOptions options)
    {
        this.httpClient = httpClient;
        this.options = options;
    }

    public async Task<AiStructuredResult> GetStructuredCompletionAsync(AiStructuredRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        var system = string.IsNullOrWhiteSpace(request.SystemInstruction) ? null : request.SystemInstruction;
        var userText = request.Prompt;

        var payload = new
        {
            model = options.Model,
            messages = system is null
                ? new object[] { new { role = "user", content = userText } }
                : new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = userText }
                },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "doc_control_structured",
                    strict = true,
                    schema = request.ResponseSchema
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        message.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return AiStructuredResult.Failure($"OpenAI error {(int)response.StatusCode}: {response.ReasonPhrase}: {raw}", raw);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return AiStructuredResult.Failure("OpenAI response missing content.", raw);
                }

                try
                {
                    using var parsed = JsonDocument.Parse(content);
                    return AiStructuredResult.Success(raw, parsed.RootElement.Clone());
                }
                catch (JsonException)
                {
                    return AiStructuredResult.Failure("OpenAI content was not valid JSON.", raw);
                }
            }

            return AiStructuredResult.Failure("OpenAI response missing choices payload.", raw);
        }
        catch (JsonException)
        {
            return AiStructuredResult.Failure("Failed to parse OpenAI response as JSON.", raw);
        }
    }
}

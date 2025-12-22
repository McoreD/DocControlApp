using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;

namespace DocControl.Api.Infrastructure;

public static class HttpResponseExtensions
{
    public static async Task<HttpResponseData> ToJsonAsync<T>(this HttpRequestData req, T payload, HttpStatusCode status = HttpStatusCode.OK, JsonSerializerOptions? options = null)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json");
        var json = JsonSerializer.Serialize(payload, options ?? DefaultJsonOptions.Instance);
        await response.WriteStringAsync(json).ConfigureAwait(false);
        return response;
    }

    public static HttpResponseData Error(this HttpRequestData req, HttpStatusCode status, string message)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json");
        var json = JsonSerializer.Serialize(new { error = message }, DefaultJsonOptions.Instance);
        response.WriteString(json);
        return response;
    }

    private sealed class DefaultJsonOptions
    {
        public static readonly JsonSerializerOptions Instance = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

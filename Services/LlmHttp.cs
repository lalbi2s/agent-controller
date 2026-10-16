using System.Text.Json;

namespace AgentController.Services;

internal static class LlmHttp
{
    private const int MaxDetailLength = 300;

    /// <summary>
    /// Throws a HttpRequestException that never contains the API key.
    /// The provider message is only added for errors that are not about authentication.
    /// </summary>
    public static async Task EnsureSuccessAsync(
        HttpResponseMessage response, string provider, string apiKey, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = $"{provider} returned HTTP {(int)response.StatusCode}";
        var isAuthError = response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden;

        if (!isAuthError)
        {
            var detail = ReadErrorMessage(await response.Content.ReadAsStringAsync(ct));
            if (detail is not null)
                message += $": {detail.Replace(apiKey, "***")}";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    // Both providers answer errors with { "error": { "message": "..." } }
    private static string? ReadErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var message = doc.RootElement.GetProperty("error").GetProperty("message").GetString();
            if (message is null)
                return null;

            return message.Length <= MaxDetailLength ? message : message[..MaxDetailLength] + "...";
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}

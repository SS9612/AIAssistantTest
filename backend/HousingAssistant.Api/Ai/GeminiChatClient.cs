using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace HousingAssistant.Api.Ai;

public sealed class GeminiChatClient : IChatAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiChatClient(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string> GenerateReplyAsync(
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new ChatAiException(
                "Assistenten är inte konfigurerad. Lägg till en Gemini API-nyckel.");
        }

        var model = Uri.EscapeDataString(_options.Model.Trim());
        var requestUri = new Uri(
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
            UriKind.Absolute);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(
            new GeminiGenerateContentRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = HousingAssistantPrompt.System }]
                },
                Contents =
                [
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = userMessage }]
                    }
                ]
            },
            options: JsonOptions);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var googleMessage = TryReadGoogleError(responseBody);
                throw new ChatAiException(
                    string.IsNullOrWhiteSpace(googleMessage)
                        ? $"Gemini svarade med HTTP {(int)response.StatusCode}."
                        : $"Gemini-fel ({(int)response.StatusCode}): {googleMessage}");
            }

            return ReadReply(responseBody);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new ChatAiException(
                "Gemini-anropet tog för lång tid.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ChatAiException(
                "Kunde inte nå Gemini just nu. Försök igen om en stund.",
                ex);
        }
    }

    private static string ReadReply(string responseBody)
    {
        GeminiGenerateContentResponse? payload;
        try
        {
            payload = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(
                responseBody,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ChatAiException(
                "Gemini svarade med ett ogiltigt format.",
                ex);
        }

        var text = payload?.Candidates?
            .SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Select(part => part.Text)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ChatAiException(
                "Gemini returnerade inget text-svar. Försök igen.");
        }

        return text.Trim();
    }

    private static string? TryReadGoogleError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore parse failures and fall back to the generic message.
        }

        return null;
    }

    private sealed class GeminiGenerateContentRequest
    {
        public GeminiContent? SystemInstruction { get; init; }

        public required IReadOnlyList<GeminiContent> Contents { get; init; }
    }

    private sealed class GeminiGenerateContentResponse
    {
        public IReadOnlyList<GeminiCandidate>? Candidates { get; init; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; init; }
    }

    private sealed class GeminiContent
    {
        public string? Role { get; init; }

        public IReadOnlyList<GeminiPart>? Parts { get; init; }
    }

    private sealed class GeminiPart
    {
        public string? Text { get; init; }
    }
}

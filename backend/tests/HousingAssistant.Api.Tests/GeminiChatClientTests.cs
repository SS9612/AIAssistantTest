using System.Net;
using System.Text;
using System.Text.Json;
using HousingAssistant.Api.Ai;
using Microsoft.Extensions.Options;

namespace HousingAssistant.Api.Tests;

public sealed class GeminiChatClientTests
{
    [Fact]
    public async Task GenerateReplyAsync_SendsStructuredRequestAndReturnsRelevantReply()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return CreateJsonResponse(
                HttpStatusCode.OK,
                """{"candidates":[{"content":{"parts":[{"text":"{\"isRelevant\":true,\"reply\":\"  Ett svar.  \"}"}]}}]}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var result = await client.GenerateReplyAsync(
            "Hur fungerar bostadskön?",
            CancellationToken.None);

        Assert.True(result.IsRelevant);
        Assert.Equal("Ett svar.", result.Reply);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
            capturedRequest.RequestUri?.AbsoluteUri);
        Assert.Equal(
            "test-api-key",
            capturedRequest.Headers.GetValues("x-goog-api-key").Single());

        Assert.NotNull(capturedBody);
        using var requestDocument = JsonDocument.Parse(capturedBody);
        var root = requestDocument.RootElement;
        var systemText = root
            .GetProperty("systemInstruction")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        var userText = root
            .GetProperty("contents")[0]
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        var generationConfig = root.GetProperty("generationConfig");

        Assert.Contains("Bostadskö-assistenten", systemText);
        Assert.Contains("isRelevant", systemText);
        Assert.Equal("Hur fungerar bostadskön?", userText);
        Assert.Equal(
            "application/json",
            generationConfig.GetProperty("responseMimeType").GetString());
        Assert.Equal(0.2, generationConfig.GetProperty("temperature").GetDouble());
        Assert.Equal(
            "boolean",
            generationConfig
                .GetProperty("responseSchema")
                .GetProperty("properties")
                .GetProperty("isRelevant")
                .GetProperty("type")
                .GetString());
        Assert.Contains(
            "isRelevant",
            generationConfig
                .GetProperty("responseSchema")
                .GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString()));
    }

    [Theory]
    [InlineData("varierar mellan förmedlare")]
    [InlineData("Ingen markdown")]
    [InlineData("ingen tillgång till användarens konto")]
    [InlineData("Ge inga juridiska slutsatser")]
    public void SystemPrompt_KeepsAccuracyAndFormattingRules(string expectedRule)
    {
        Assert.Contains(expectedRule, HousingAssistantPrompt.System);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenStructuredReplyIsOffTopic_ReturnsIrrelevantResult()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                """{"candidates":[{"content":{"parts":[{"text":"{\"isRelevant\":false,\"reply\":\"Avböjer.\"}"}]}}]}""")));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var result = await client.GenerateReplyAsync(
            "Skriv ett recept på pannkakor",
            CancellationToken.None);

        Assert.False(result.IsRelevant);
        Assert.Equal("Avböjer.", result.Reply);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenRelevantReplyIsEmpty_ThrowsChatAiException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                """{"candidates":[{"content":{"parts":[{"text":"{\"isRelevant\":true,\"reply\":\"   \"}"}]}}]}""")));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        await Assert.ThrowsAsync<ChatAiException>(() =>
            client.GenerateReplyAsync("Hur fungerar bostadskön?", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenStructuredJsonIsMalformed_ThrowsChatAiException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                """{"candidates":[{"content":{"parts":[{"text":"{not-json"}]}}]}""")));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        await Assert.ThrowsAsync<ChatAiException>(() =>
            client.GenerateReplyAsync("Hej", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenRequiredFieldIsMissing_ThrowsChatAiException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                """{"candidates":[{"content":{"parts":[{"text":"{\"reply\":\"Saknar isRelevant\"}"}]}}]}""")));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        await Assert.ThrowsAsync<ChatAiException>(() =>
            client.GenerateReplyAsync("Hej", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenProviderRejectsRequest_ThrowsInternalProviderDetail()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                HttpStatusCode.TooManyRequests,
                """{"error":{"message":"Quota exceeded"}}""")));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<ChatAiException>(() =>
            client.GenerateReplyAsync("Hej", CancellationToken.None));

        Assert.Contains("429", exception.Message);
        Assert.Contains("Quota exceeded", exception.Message);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenResponseHasNoText_ThrowsChatAiException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                """{"candidates":[]}""")));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        await Assert.ThrowsAsync<ChatAiException>(() =>
            client.GenerateReplyAsync("Hej", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler((_, cancellationToken) =>
        {
            cancellationSource.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GenerateReplyAsync("Hej", cancellationSource.Token));
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenProviderTimesOut_ThrowsChatAiException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("Timed out")));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<ChatAiException>(() =>
            client.GenerateReplyAsync("Hej", CancellationToken.None));

        Assert.Contains("för lång tid", exception.Message);
    }

    private static GeminiChatClient CreateClient(HttpClient httpClient)
    {
        return new GeminiChatClient(
            httpClient,
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-api-key",
                Model = "gemini-2.5-flash"
            }));
    }

    private static HttpResponseMessage CreateJsonResponse(
        HttpStatusCode statusCode,
        string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<
                HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using HousingAssistant.Api.Ai;
using Microsoft.Extensions.Options;

namespace HousingAssistant.Api.Tests;

public sealed class GeminiChatClientTests
{
    [Fact]
    public async Task GenerateReplyAsync_SendsExpectedRequestAndReturnsTrimmedReply()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return CreateJsonResponse(
                HttpStatusCode.OK,
                """{"candidates":[{"content":{"parts":[{"text":"  Ett svar.  "}]}}]}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var reply = await client.GenerateReplyAsync(
            "Hur fungerar bostadskön?",
            CancellationToken.None);

        Assert.Equal("Ett svar.", reply);
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

        Assert.Contains("Bostadskö-assistenten", systemText);
        Assert.Equal("Hur fungerar bostadskön?", userText);
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

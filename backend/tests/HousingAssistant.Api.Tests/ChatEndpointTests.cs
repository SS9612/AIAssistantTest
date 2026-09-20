using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HousingAssistant.Api.Ai;
using HousingAssistant.Api.Chat;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HousingAssistant.Api.Tests;

public sealed class ChatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AssistantReply =
        "Hej! Jag är Bostadskö-assistenten. Hur kan jag hjälpa dig?";

    private readonly WebApplicationFactory<Program> _factory;

    public ChatEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WithValidMessage_ReturnsAssistantReply()
    {
        var client = CreateClient(new FakeChatAiClient());

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = "Hur fungerar bostadskön?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal(AssistantReply, body.Reply);
    }

    [Fact]
    public async Task Post_WhenOffTopic_ReturnsFixedOutOfScopeReply()
    {
        var client = CreateClient(new FakeChatAiClient(
            result: new ChatAiResult
            {
                IsRelevant = false,
                Reply = "This provider text must be discarded."
            }));

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = "Skriv ett recept på pannkakor"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal(HousingAssistantPrompt.OutOfScopeReply, body.Reply);
        Assert.DoesNotContain("discarded", body.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_WithEmptyOrWhitespaceMessage_ReturnsBadRequest(string message)
    {
        var client = CreateClient(new FakeChatAiClient());

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = message
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Message", out _));
    }

    [Fact]
    public async Task Post_WithMalformedJson_ReturnsBadRequest()
    {
        var client = CreateClient(new FakeChatAiClient());
        using var content = new StringContent("""{"message":}""", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/chat", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenAiClientIsNotConfigured_ReturnsServiceUnavailable()
    {
        var client = CreateClient(new FakeChatAiClient(isConfigured: false));

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = "Hur fungerar bostadskön?"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Gemini", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_WhenAiProviderFails_ReturnsSanitizedBadGateway()
    {
        var client = CreateClient(new FakeChatAiClient(
            exception: new ChatAiException("Sensitive provider detail")));

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = "Hur fungerar bostadskön?"
        });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Sensitive provider detail", body);
        Assert.DoesNotContain("Gemini", body, StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateClient(IChatAiClient chatAiClient)
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChatAiClient>();
                    services.AddSingleton(chatAiClient);
                });
            })
            .CreateClient();
    }

    private sealed class FakeChatAiClient : IChatAiClient
    {
        private readonly Exception? _exception;
        private readonly ChatAiResult _result;

        public FakeChatAiClient(
            bool isConfigured = true,
            Exception? exception = null,
            ChatAiResult? result = null)
        {
            IsConfigured = isConfigured;
            _exception = exception;
            _result = result ?? new ChatAiResult
            {
                IsRelevant = true,
                Reply = AssistantReply
            };
        }

        public bool IsConfigured { get; }

        public Task<ChatAiResult> GenerateReplyAsync(
            string userMessage,
            CancellationToken cancellationToken)
        {
            return _exception is null
                ? Task.FromResult(_result)
                : Task.FromException<ChatAiResult>(_exception);
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HousingAssistant.Api.Chat;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HousingAssistant.Api.Tests;

public sealed class ChatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChatEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_WithValidMessage_ReturnsHardcodedSwedishReply()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = "Hur fungerar bostadskön?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal(
            "Hej! Jag är Bostadskö-assistenten. Hur kan jag hjälpa dig?",
            body.Reply);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_WithEmptyOrWhitespaceMessage_ReturnsBadRequest(string message)
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
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
        using var content = new StringContent("""{"message":}""", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/chat", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using HousingAssistant.Api.Ai;
using Microsoft.AspNetCore.Mvc;

namespace HousingAssistant.Api.Chat;

[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatAiClient _chatAiClient;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatAiClient chatAiClient,
        ILogger<ChatController> logger)
    {
        _chatAiClient = chatAiClient;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ChatResponse>> Post(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!_chatAiClient.IsConfigured)
        {
            _logger.LogWarning("The chat AI client is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Assistenten är inte tillgänglig",
                detail: "Assistenten kan inte användas just nu. Försök igen senare.");
        }

        try
        {
            var result = await _chatAiClient.GenerateReplyAsync(
                request.Message,
                cancellationToken);

            var reply = result.IsRelevant
                ? result.Reply
                : HousingAssistantPrompt.OutOfScopeReply;

            return Ok(new ChatResponse { Reply = reply });
        }
        catch (ChatAiException ex)
        {
            _logger.LogWarning(ex, "The chat AI provider request failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Kunde inte hämta svar från AI-tjänsten",
                detail: "Assistenten kunde inte generera ett svar. Försök igen om en stund.");
        }
    }
}

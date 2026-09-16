using Microsoft.AspNetCore.Mvc;

namespace HousingAssistant.Api.Chat;

[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public sealed class ChatController : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<ChatResponse> Post([FromBody] ChatRequest request)
    {
        return Ok(new ChatResponse
        {
            Reply = "Hej! Jag är Bostadskö-assistenten. Hur kan jag hjälpa dig?"
        });
    }
}

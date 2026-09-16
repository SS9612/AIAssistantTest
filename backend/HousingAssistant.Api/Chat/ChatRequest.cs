using System.ComponentModel.DataAnnotations;

namespace HousingAssistant.Api.Chat;

public sealed class ChatRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

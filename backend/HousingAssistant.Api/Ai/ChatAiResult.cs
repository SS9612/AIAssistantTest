namespace HousingAssistant.Api.Ai;

public sealed class ChatAiResult
{
    public required bool IsRelevant { get; init; }

    public required string Reply { get; init; }
}

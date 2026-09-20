namespace HousingAssistant.Api.Ai;

public interface IChatAiClient
{
    bool IsConfigured { get; }

    Task<ChatAiResult> GenerateReplyAsync(string userMessage, CancellationToken cancellationToken);
}

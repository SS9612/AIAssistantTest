namespace HousingAssistant.Api.Ai;

public interface IChatAiClient
{
    bool IsConfigured { get; }

    Task<string> GenerateReplyAsync(string userMessage, CancellationToken cancellationToken);
}

namespace HousingAssistant.Api.Ai;

public sealed class ChatAiException : Exception
{
    public ChatAiException(string message) : base(message)
    {
    }

    public ChatAiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

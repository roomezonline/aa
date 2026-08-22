namespace AiAssistant.Core.Interfaces;

public interface IChatService
{
    IAsyncEnumerable<string> StreamChatAsync(
        string userMessage, 
        string conversationId,
        CancellationToken cancellationToken = default);
}

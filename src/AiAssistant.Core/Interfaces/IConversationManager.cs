namespace AiAssistant.Core.Interfaces;

public interface IConversationManager
{
    Task<string> CreateConversationAsync(string userId, string title = "");
    Task AddMessageAsync(string conversationId, string role, string content);
    Task<List<ConversationMessage>> GetHistoryAsync(string conversationId, int maxMessages = 20);
    Task<List<ConversationInfo>> GetAllConversationsAsync(string userId);
    Task<ConversationInfo?> GetConversationAsync(string conversationId);
}

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ConversationInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MessageCount { get; set; }
}

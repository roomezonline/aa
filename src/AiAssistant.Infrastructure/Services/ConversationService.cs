using AiAssistant.Core.Interfaces;
using AiAssistant.Core.Models;
using AiAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.Infrastructure.Services;

public class ConversationService : IConversationManager
{
    private readonly Func<AppDbContext> _contextFactory;

    public ConversationService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<string> CreateConversationAsync(string title = "")
    {
        await using var context = _contextFactory();
        var conversation = new ConversationEntity
        {
            Id = Guid.NewGuid().ToString(),
            Title = string.IsNullOrWhiteSpace(title) ? "مکالمه جدید" : title,
            CreatedAt = DateTime.UtcNow
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
        return conversation.Id;
    }

    public async Task AddMessageAsync(
        string conversationId, string role, string content)
    {
        await using var context = _contextFactory();
        var message = new MessageEntity
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();
    }

    public async Task<List<ConversationMessage>> GetHistoryAsync(
        string conversationId, int maxMessages = 20)
    {
        await using var context = _contextFactory();
        return await context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxMessages)
            .Select(m => new ConversationMessage
            {
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            })
            .Reverse()
            .ToListAsync();
    }

    public async Task<List<ConversationInfo>> GetAllConversationsAsync()
    {
        await using var context = _contextFactory();
        return await context.Conversations
            .Select(c => new ConversationInfo
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                MessageCount = c.Messages.Count
            })
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}

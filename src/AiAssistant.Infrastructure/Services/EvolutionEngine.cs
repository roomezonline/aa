using AiAssistant.Core.Interfaces;
using AiAssistant.Core.Models;
using AiAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.Infrastructure.Services;

public class EvolutionEngine : IEvolutionEngine
{
    private readonly ISearchService _searchService;
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly IEmbeddingService _embeddingService;
    private readonly Func<AppDbContext> _contextFactory;

    public EvolutionEngine(
        ISearchService searchService,
        IKnowledgeBase knowledgeBase,
        IEmbeddingService embeddingService,
        Func<AppDbContext> contextFactory)
    {
        _searchService = searchService;
        _knowledgeBase = knowledgeBase;
        _embeddingService = embeddingService;
        _contextFactory = contextFactory;
    }

    public async Task<string> GetSmartResponseAsync(
        string userMessage, float[] embedding, CancellationToken ct = default)
    {
        await QueueUnknownTopicAsync(userMessage, embedding, ct);

        var result = await SearchAndLearnAsync(userMessage, embedding, ct);
        if (result != null) return result;

        return await HandleUnknownAsync(userMessage, ct);
    }

    public async Task<List<LearningQueueItem>> GetPendingQueueAsync(CancellationToken ct = default)
    {
        await using var ctx = _contextFactory();
        return await ctx.LearningQueue
            .Where(q => q.Status == "pending")
            .OrderByDescending(q => q.Priority)
            .ThenBy(q => q.TimesFailed)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<int> GetQueueCountAsync(CancellationToken ct = default)
    {
        await using var ctx = _contextFactory();
        return await ctx.LearningQueue.CountAsync(q => q.Status == "pending", ct);
    }

    public async Task StartEvolutionAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var pending = await GetPendingQueueAsync(ct);
            if (pending.Count == 0)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
                continue;
            }

            foreach (var item in pending)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var embedding = await _embeddingService.GetEmbeddingAsync(item.Question, ct);

                    var results = await _searchService.SearchAsync(
                        string.IsNullOrWhiteSpace(item.Question) ? item.Topic : item.Question, 5, ct);

                    if (results.Any())
                    {
                        var answer = BuildAnswer(results);

                        await _knowledgeBase.SaveAsync(new KnowledgeEntry
                        {
                            Question = item.Question,
                            Answer = answer,
                            Source = "evolution",
                            Topic = item.Topic,
                            Embedding = embedding,
                            Confidence = 0.85,
                            CreatedAt = DateTime.UtcNow
                        });

                        await UpdateQueueStatusAsync(item.Id, "completed", ct);
                    }
                    else
                    {
                        item.TimesFailed++;
                        item.LastAttemptAt = DateTime.UtcNow;
                        item.Priority = Math.Min(item.Priority + 1, 10);
                        await SaveQueueItemAsync(item, ct);
                    }
                }
                catch
                {
                    item.TimesFailed++;
                    item.LastAttemptAt = DateTime.UtcNow;
                    await SaveQueueItemAsync(item, ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    private async Task QueueUnknownTopicAsync(
        string userMessage, float[] embedding, CancellationToken ct)
    {
        try
        {
            await using var ctx = _contextFactory();
            var topic = ExtractTopic(userMessage);

            var exists = await ctx.LearningQueue.AnyAsync(
                q => q.Question == userMessage && q.Status == "pending", ct);

            if (!exists)
            {
                ctx.LearningQueue.Add(new LearningQueueItem
                {
                    Topic = topic,
                    Question = userMessage,
                    Priority = 5,
                    TimesFailed = 0,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync(ct);
            }
        }
        catch { }
    }

    private async Task<string?> SearchAndLearnAsync(
        string userMessage, float[] embedding, CancellationToken ct)
    {
        try
        {
            var results = await _searchService.SearchAsync(userMessage, 5, ct);
            if (!results.Any()) return null;

            var answer = BuildAnswer(results);

            await _knowledgeBase.SaveAsync(new KnowledgeEntry
            {
                Question = userMessage,
                Answer = answer,
                Source = "evolution_search",
                Embedding = embedding,
                Confidence = 0.85,
                CreatedAt = DateTime.UtcNow
            });

            var queueItem = await FindAndCompleteQueueItemAsync(userMessage, ct);

            return answer;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> HandleUnknownAsync(string userMessage, CancellationToken ct)
    {
        try
        {
            var results = await _searchService.SearchAsync(userMessage, 3, ct);
            if (results.Any())
            {
                return BuildAnswer(results);
            }
        }
        catch { }

        return $"هنوز اطلاعاتی درباره «{userMessage}» ندارم. در حال یادگیری هستم...";
    }

    private static string ExtractTopic(string message)
    {
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 3) return message;

        var importantWords = words
            .Where(w => w.Length > 2)
            .Take(3)
            .ToArray();

        return string.Join(" ", importantWords);
    }

    private static string BuildAnswer(List<SearchResult> results)
    {
        var answer = "";
        foreach (var r in results.Take(3))
        {
            answer += $"**{r.Title}**\n{r.Snippet}\n\n";
        }
        return answer.TrimEnd();
    }

    private async Task<LearningQueueItem?> FindAndCompleteQueueItemAsync(
        string question, CancellationToken ct)
    {
        try
        {
            await using var ctx = _contextFactory();
            var item = await ctx.LearningQueue
                .FirstOrDefaultAsync(q => q.Question == question && q.Status == "pending", ct);

            if (item != null)
            {
                item.Status = "completed";
                item.CompletedAt = DateTime.UtcNow;
                await ctx.SaveChangesAsync(ct);
            }
            return item;
        }
        catch { return null; }
    }

    private async Task UpdateQueueStatusAsync(
        int id, string status, CancellationToken ct)
    {
        try
        {
            await using var ctx = _contextFactory();
            var item = await ctx.LearningQueue.FindAsync(id, ct);
            if (item != null)
            {
                item.Status = status;
                item.CompletedAt = DateTime.UtcNow;
                await ctx.SaveChangesAsync(ct);
            }
        }
        catch { }
    }

    private async Task SaveQueueItemAsync(LearningQueueItem item, CancellationToken ct)
    {
        try
        {
            await using var ctx = _contextFactory();
            ctx.LearningQueue.Update(item);
            await ctx.SaveChangesAsync(ct);
        }
        catch { }
    }
}

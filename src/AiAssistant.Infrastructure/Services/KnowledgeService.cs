using AiAssistant.Core.Interfaces;
using AiAssistant.Core.Models;
using AiAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.Infrastructure.Services;

public class KnowledgeService : IKnowledgeBase
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;

    public KnowledgeService(
        Func<AppDbContext> contextFactory,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService)
    {
        _contextFactory = contextFactory;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
    }

    public async Task<KnowledgeEntry?> FindBestMatchAsync(
        string question, float[] questionEmbedding)
    {
        var results = await _vectorStore.SearchAsync(questionEmbedding, topK: 1);
        return results.FirstOrDefault()?.Score > 0.85
            ? new KnowledgeEntry
            {
                Question = question,
                Answer = results.First().Content,
                Confidence = results.First().Score
            }
            : null;
    }

    public async Task SaveAsync(KnowledgeEntry entry)
    {
        await using var context = _contextFactory();
        context.Knowledge.Add(entry);
        await context.SaveChangesAsync();

        if (entry.Embedding == null)
        {
            entry.Embedding = await _embeddingService.GetEmbeddingAsync(entry.Question);
        }

        await _vectorStore.SaveAsync(new VectorDocument
        {
            Id = entry.Id.ToString(),
            Content = entry.Answer,
            Embedding = entry.Embedding,
            Metadata = new Dictionary<string, string>
            {
                ["question"] = entry.Question,
                ["source"] = entry.Source
            }
        });
    }

    public async Task<List<KnowledgeEntry>> GetByTopicAsync(
        string topic, int limit = 100)
    {
        await using var context = _contextFactory();
        return await context.Knowledge
            .Where(k => k.Topic == topic)
            .OrderByDescending(k => k.UseCount)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<KnowledgeEntry>> SearchAsync(
        string query, float[] queryEmbedding, int topK = 5)
    {
        var vectorResults = await _vectorStore.SearchAsync(queryEmbedding, topK);
        var ids = vectorResults.Select(r => int.Parse(r.Id)).ToList();

        await using var context = _contextFactory();
        return await context.Knowledge
            .Where(k => ids.Contains(k.Id))
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        await using var context = _contextFactory();
        return await context.Knowledge.CountAsync();
    }
}

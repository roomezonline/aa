using AiAssistant.Core.Interfaces;
using AiAssistant.Core.Models;

namespace AiAssistant.Infrastructure.Services;

public class SelfLearnerService : ISelfLearner
{
    private readonly IChatService _chatService;
    private readonly ISearchService _searchService;
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly IEmbeddingService _embeddingService;

    public SelfLearnerService(
        IChatService chatService,
        ISearchService searchService,
        IKnowledgeBase knowledgeBase,
        IEmbeddingService embeddingService)
    {
        _chatService = chatService;
        _searchService = searchService;
        _knowledgeBase = knowledgeBase;
        _embeddingService = embeddingService;
    }

    public async Task StartLearningAsync(
        string topic,
        TimeSpan duration,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow.Add(duration);

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            var questions = GenerateQuestions(topic);

            foreach (var question in questions)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (DateTime.UtcNow >= endTime) break;

                progress?.Report($"سرچ: {question}");

                var results = await _searchService.SearchAsync(question, 3, cancellationToken);
                if (results.Count == 0) continue;

                var bestResult = results.First();
                var embedding = await _embeddingService.GetEmbeddingAsync(
                    question, cancellationToken);

                await _knowledgeBase.SaveAsync(new KnowledgeEntry
                {
                    Question = question,
                    Answer = $"({bestResult.Title}) {bestResult.Snippet}",
                    Source = "self_learning",
                    Topic = topic,
                    Embedding = embedding,
                    Confidence = 0.8,
                    CreatedAt = DateTime.UtcNow
                });

                progress?.Report($"ذخیره شد: {question}");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        progress?.Report("یادگیری تمام شد!");
    }

    private static List<string> GenerateQuestions(string topic)
    {
        return new List<string>
        {
            $"{topic} چیست؟",
            $"انواع {topic} کدامند؟",
            $"مزایای {topic} چیست؟",
            $"معایب {topic} چیست؟",
            $"کاربردهای {topic} در زندگی واقعی",
            $"آخرین پیشرفت‌های {topic}",
            $"ابزارهای مرتبط با {topic}",
            $"آینده {topic} چگونه است؟",
            $"بهترین منابع یادگیری {topic}",
            $"نمونه‌های عملی {topic}"
        };
    }
}

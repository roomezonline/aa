using AiAssistant.Core.Interfaces;
using AiAssistant.Core.Models;

namespace AiAssistant.Infrastructure.Services;

public class LocalAiEngine : IChatService
{
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly ISearchService _searchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IConversationManager _conversationManager;
    private readonly IEvolutionEngine _evolution;

    private static readonly Random _random = new();

    private static readonly Dictionary<string, List<string>> _patterns = new()
    {
        ["greeting"] = new() { "سلام", "درود", "هلو", "خسته نباشید", "صبح بخیر", "خوبی" },
        ["farewell"] = new() { "خداحافظ", "بای", "فعلاً", "تا بعد", "موفق باشید" },
        ["thanks"] = new() { "ممنون", "متشکرم", "مرسی", "تشکر", "سپاس" },
        ["identity"] = new() { "کی هستی", "چی هستی", "اسمت چیه", "معرفی کن" },
        ["time"] = new() { "ساعت چنده", "ساعت", "تاریخ امروز" },
        ["joke"] = new() { "جوک", "لطیفه", "خنده", "بامزه" },
    };

    private static readonly Dictionary<string, List<string>> _responses = new()
    {
        ["greeting"] = new() { "سلام! خوش آمدید. چطور می‌تونم کمکتون کنم؟", "درود! حالتون چطوره؟ در خدمتم!" },
        ["farewell"] = new() { "خداحافظ! موفق باشید!", "فعلاً! هر وقت سوالی داشتید در خدمتم." },
        ["thanks"] = new() { "خواهش می‌کنم!", "خواهش! هر وقت کمک خواستید در خدمتم." },
        ["identity"] = new() { "من دستیار هوش مصنوعی شما هستم. هر چی بیشتر ازم بپرسید، باهوش‌تر می‌شم!" },
        ["time"] = new() { $"الان ساعت {DateTime.Now:HH:mm} و تاریخ {DateTime.Now:yyyy/MM/dd} هست." },
        ["joke"] = new() { "چرا برنامه‌نویس عینک می‌زنه؟ چون C# نمی‌بینه!", "یه هوش مصنوعی میره کافه، صاحبش میگه چی می‌خوری؟ هوش مصنوعی میگه: دیتای شما رو!" },
    };

    public LocalAiEngine(
        IKnowledgeBase knowledgeBase,
        ISearchService searchService,
        IEmbeddingService embeddingService,
        IConversationManager conversationManager,
        IEvolutionEngine evolution)
    {
        _knowledgeBase = knowledgeBase;
        _searchService = searchService;
        _embeddingService = embeddingService;
        _conversationManager = conversationManager;
        _evolution = evolution;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string userMessage,
        string conversationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _conversationManager.AddMessageAsync(conversationId, "user", userMessage);

        await foreach (var token in GenerateStreamingResponseAsync(userMessage, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;
            yield return token;
        }

        var fullResponse = await GenerateFullResponseAsync(userMessage);
        await _conversationManager.AddMessageAsync(conversationId, "assistant", fullResponse);
    }

    private async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var detectedPattern = DetectPattern(userMessage);

        if (detectedPattern != null && _responses.ContainsKey(detectedPattern))
        {
            var response = _responses[detectedPattern][_random.Next(_responses[detectedPattern].Count)];

            if (detectedPattern == "time")
            {
                yield return response;
                yield break;
            }

            foreach (var token in FormatStreamingText(response))
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                yield return token;
            }
            yield break;
        }

        var embedding = await _embeddingService.GetEmbeddingAsync(userMessage);
        var knowledgeMatch = await _knowledgeBase.FindBestMatchAsync(userMessage, embedding);

        if (knowledgeMatch != null)
        {
            foreach (var token in FormatStreamingText(knowledgeMatch.Answer))
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                yield return token;
            }
            yield break;
        }

        yield return "@@status:در حال جستجو در اینترنت...";
        await Task.Delay(300, cancellationToken);

        List<SearchResult> allResults = new();
        var queries = GenerateSearchQueries(userMessage);

        foreach (var query in queries)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            yield return $"@@search:{query}";
            await Task.Delay(200, cancellationToken);

            var results = await SearchSafeAsync(query, 3, cancellationToken);
            if (results.Any())
            {
                allResults.AddRange(results);
                foreach (var r in results.Take(2))
                {
                    yield return $"@@result:{r.Title}|{Truncate(r.Snippet, 120)}";
                    await Task.Delay(150, cancellationToken);
                }
            }
        }

        if (!allResults.Any())
        {
            foreach (var token in FormatStreamingText($"متأسفانه نتیجه‌ای برای «{userMessage}» پیدا نکردم. لطفاً سوال خود را با کلمات دیگری مطرح کنید."))
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                yield return token;
            }
            yield break;
        }

        yield return "@@status:در حال تحلیل و جمع‌بندی نتایج...";
        await Task.Delay(500, cancellationToken);

        var finalAnswer = CompileBestAnswer(userMessage, allResults);

        foreach (var token in FormatStreamingText(finalAnswer))
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            yield return token;
        }

        await SaveKnowledgeSafeAsync(userMessage, finalAnswer, embedding);
    }

    private async Task<List<SearchResult>> SearchSafeAsync(string query, int maxResults, CancellationToken ct)
    {
        try
        {
            return await _searchService.SearchAsync(query, maxResults, ct);
        }
        catch
        {
            return new List<SearchResult>();
        }
    }

    private async Task SaveKnowledgeSafeAsync(string question, string answer, float[] embedding)
    {
        try
        {
            await _knowledgeBase.SaveAsync(new KnowledgeEntry
            {
                Question = question,
                Answer = answer,
                Source = "evolution_search",
                Embedding = embedding,
                Confidence = 0.85,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch { }
    }

    private List<string> GenerateSearchQueries(string userMessage)
    {
        var queries = new List<string> { userMessage };

        var words = userMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 3)
        {
            var shortQuery = string.Join(" ", words.Take(4));
            if (shortQuery != userMessage)
                queries.Add(shortQuery);
        }

        if (userMessage.Contains("چیست") || userMessage.Contains("چیه"))
        {
            queries.Add(userMessage.Replace("چیست", "").Replace("چیه", "").Trim());
        }

        if (userMessage.Contains("مقایسه") || userMessage.Contains("vs") || userMessage.Contains("یا"))
        {
            queries.Add(userMessage + " مزایای معایب");
        }

        return queries.Take(3).ToList();
    }

    private static string CompileBestAnswer(string question, List<SearchResult> results)
    {
        var uniqueResults = results
            .GroupBy(r => r.Title)
            .Select(g => g.First())
            .Take(5)
            .ToList();

        var answer = "";

        if (uniqueResults.Count > 1)
        {
            answer += $"بر اساس {uniqueResults.Count} منبع مختلف:\n\n";
        }

        foreach (var r in uniqueResults)
        {
            var snippet = r.Snippet;
            if (!snippet.EndsWith(".") && !snippet.EndsWith("!") && !snippet.EndsWith("?"))
                snippet += "...";

            answer += $"**{r.Title}**\n{snippet}\n\n";
        }

        return answer.TrimEnd();
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLen ? text : text[..maxLen] + "...";
    }

    private async Task<string> GenerateFullResponseAsync(string userMessage)
    {
        var detectedPattern = DetectPattern(userMessage);

        if (detectedPattern != null && _responses.ContainsKey(detectedPattern))
        {
            return _responses[detectedPattern][_random.Next(_responses[detectedPattern].Count)];
        }

        var embedding = await _embeddingService.GetEmbeddingAsync(userMessage);
        var knowledgeMatch = await _knowledgeBase.FindBestMatchAsync(userMessage, embedding);

        if (knowledgeMatch != null)
        {
            return knowledgeMatch.Answer;
        }

        return await _evolution.GetSmartResponseAsync(userMessage, embedding);
    }

    private static IEnumerable<string> FormatStreamingText(string text)
    {
        var buffer = "";
        foreach (var c in text)
        {
            buffer += c;
            if (c == ' ' || c == '\n' || buffer.Length >= 3)
            {
                yield return buffer;
                buffer = "";
            }
        }
        if (buffer.Length > 0)
            yield return buffer;
    }

    private string? DetectPattern(string message)
    {
        var normalized = message.Trim().ToLower();
        foreach (var pattern in _patterns)
        {
            foreach (var keyword in pattern.Value)
            {
                if (normalized.Contains(keyword))
                    return pattern.Key;
            }
        }
        return null;
    }
}

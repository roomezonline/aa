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

            if (detectedPattern == "weather")
            {
                yield return "\n\n";
                var searchResults = await _searchService.SearchAsync($"آب و هوا {userMessage}", 3);
                if (searchResults.Any())
                {
                    foreach (var r in searchResults.Take(2))
                    {
                        foreach (var token in FormatStreamingText($"- **{r.Title}**: {r.Snippet}\n"))
                        {
                            if (cancellationToken.IsCancellationRequested) yield break;
                            yield return token;
                        }
                    }
                }
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

        yield return "**";
        await Task.Delay(400, cancellationToken);

        var smartResponse = await _evolution.GetSmartResponseAsync(userMessage, embedding, cancellationToken);
        foreach (var token in FormatStreamingText(smartResponse))
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            yield return token;
        }
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

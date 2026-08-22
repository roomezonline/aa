using AiAssistant.Core.Interfaces;
using System.Text.RegularExpressions;

namespace AiAssistant.Infrastructure.Services;

public class LocalAiEngine : IChatService
{
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly ISearchService _searchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IConversationManager _conversationManager;

    private static readonly Dictionary<string, List<string>> _patterns = new()
    {
        ["greeting"] = new() { "سلام", "درود", "هلو", "خسته نباشید", "صبح بخیر", "شب بخیر", "حالت چطوره", "خوبی" },
        ["farewell"] = new() { "خداحافظ", "بای", "فعلاً", "تا بعد", "موفق باشید" },
        ["thanks"] = new() { "ممنون", "متشکرم", "مرسی", "تشکر", "سپاس" },
        ["identity"] = new() { "کی هستی", "چی هستی", "اسمت چیه", "معرفی کن", "خودت رو معرفی کن" },
        ["help"] = new() { "کمک", "کمکم کن", "راهنمایی", "چطوری", "چطور استفاده کنم" },
        ["time"] = new() { "ساعت", "زمان", "چند ساعته", "تاریخ", "امروز" },
        ["weather"] = new() { "هوا", "آب و هوا", "دمای", "باران", "آفتاب" },
        ["joke"] = new() { "جوک", "لطیفه", "خنده", "بامزه", "چیز خنده دار" },
        ["search"] = new() { "سرچ", "جستجو", "گوگل", "اینترنت", "پیدا کن", "search", "find" },
    };

    private static readonly Dictionary<string, List<string>> _responses = new()
    {
        ["greeting"] = new() { "سلام! خوش آمدید. چطور می‌تونم کمکتون کنم؟ 😊", "درود! حالتون چطوره؟ در خدمتم!", "سلام! امیدوارم حالتون خوب باشه. بفرمایید چطور کمکتون کنم؟" },
        ["farewell"] = new() { "خداحافظ! موفق باشید! 👋", "فعلاً! هر وقت سوالی داشتید در خدمتم.", "بای! روز خوبی داشته باشید!" },
        ["thanks"] = new() { "خواهش می‌کنم! 😊", "خواهش! هر وقت کمک خواستید در خدمتم.", "منonym! در خدمت شما هستم." },
        ["identity"] = new() { "من دستیار هوش مصنوعی شما هستم. با پایگاه دانش خودم کار می‌کنم و می‌تونم به سوالاتتون جواب بدم. هر چی بیشتر ازم بپرسید، باهوش‌تر می‌شم! 🧠", "من یک هوش مصنوعی مستقل هستم که روی سیستم شما اجرا می‌شه. دیتابیس دارم و از اینترنت هم می‌تونم سرچ کنم.", "اسم من دستیار هوش مصنوعیه. کاملاً مستقل هستم و روی کامپیوتر شما اجرا می‌شم." },
        ["help"] = new() { "بله! می‌تونم:\n- به سوالاتتون جواب بدم\n- در اینترنت سرچ کنم\n- اطلاعات رو یاد بگیرم و ذخیره کنم\n- درباره هر موضوعی تحقیق کنم\n\nفقط کافیه سوالتون رو بنویسید!", "من می‌تونم کمکتون کنم! سوالی بپرسید، سرچ کنم، یا موضوعی برای تحقیق بهم بدید." },
        ["time"] = new() { $"الان ساعت {DateTime.Now:HH:mm} تاریخ {DateTime.Now:yyyy/MM/dd} هست.", $"ساعت الان {DateTime.Now:HH:mm} دقیقه هست. تاریخ امروز {DateTime.Now.ToLongDateString()}." },
        ["weather"] = new() { "برای اطلاع از آب و هوا، لطفاً شهرتون رو بگید تا سرچ کنم.", "آب و هوا رو نمی‌دونم ولی می‌تونم سرچ کنم! شهرتون کجاست؟" },
        ["joke"] = new() { "چرا برنامه‌نویس عینک می‌زنه؟ چون C# نمی‌بینه! 😄", "یه هوش مصنوعی میره کافه، صاحبش میگه چی می‌خوری؟ هوش مصنوعی میگه: دیتای شما رو! 😂", "چرا کامپیوتر به دکتر رفت؟ چون ویروس گرفته بود! 😄" },
    };

    public LocalAiEngine(
        IKnowledgeBase knowledgeBase,
        ISearchService searchService,
        IEmbeddingService embeddingService,
        IConversationManager conversationManager)
    {
        _knowledgeBase = knowledgeBase;
        _searchService = searchService;
        _embeddingService = embeddingService;
        _conversationManager = conversationManager;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string userMessage,
        string conversationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _conversationManager.AddMessageAsync(conversationId, "user", userMessage);

        var response = await GenerateResponseAsync(userMessage);

        await _conversationManager.AddMessageAsync(conversationId, "assistant", response);

        var words = response.Split(' ');
        foreach (var word in words)
        {
            if (cancellationToken.IsCancellationRequested) break;
            yield return word + " ";
            await Task.Delay(30, cancellationToken);
        }
    }

    private async Task<string> GenerateResponseAsync(string userMessage)
    {
        var detectedPattern = DetectPattern(userMessage);

        if (detectedPattern != null && _responses.ContainsKey(detectedPattern))
        {
            var random = new Random();
            var response = _responses[detectedPattern][random.Next(_responses[detectedPattern].Count)];

            if (detectedPattern == "weather")
            {
                var searchResults = await _searchService.SearchAsync($"آب و هوا {userMessage}", 3);
                if (searchResults.Any())
                {
                    response += "\n\nنتایج سرچ:\n";
                    foreach (var r in searchResults.Take(2))
                    {
                        response += $"- {r.Title}: {r.Snippet}\n";
                    }
                }
            }

            return response;
        }

        var embedding = await _embeddingService.GetEmbeddingAsync(userMessage);
        var knowledgeMatch = await _knowledgeBase.FindBestMatchAsync(userMessage, embedding);

        if (knowledgeMatch != null)
        {
            return $"بر اساس اطلاعات ذخیره شده:\n\n{knowledgeMatch.Answer}";
        }

        if (IsSearchQuery(userMessage))
        {
            var searchResults = await _searchService.SearchAsync(userMessage, 5);
            if (searchResults.Any())
            {
                var bestResult = searchResults.First();
                var answer = $"یافته‌ها از اینترنت:\n\n";
                foreach (var r in searchResults.Take(3))
                {
                    answer += $"**{r.Title}**\n{r.Snippet}\n\n";
                }

                await _knowledgeBase.SaveAsync(new Core.Models.KnowledgeEntry
                {
                    Question = userMessage,
                    Answer = answer,
                    Source = "web_search",
                    Embedding = embedding,
                    Confidence = 0.7
                });

                return answer;
            }
        }

        return GenerateSmartResponse(userMessage);
    }

    private string? DetectPattern(string message)
    {
        var normalized = message.Trim().ToLower();

        foreach (var pattern in _patterns)
        {
            foreach (var keyword in pattern.Value)
            {
                if (normalized.Contains(keyword.ToLower()))
                {
                    return pattern.Key;
                }
            }
        }

        return null;
    }

    private bool IsSearchQuery(string message)
    {
        var searchKeywords = new[] { "چیست", "چیه", "کیست", "کیه", "کجاست", "چگونه", "چطور", "چرا", "آیا", "تاریخ", "قیمت", "امروز" };
        return searchKeywords.Any(k => message.Contains(k));
    }

    private string GenerateSmartResponse(string userMessage)
    {
        var normalized = userMessage.Trim().ToLower();

        if (normalized.EndsWith("?") || normalized.Contains("چی") || normalized.Contains("کی") || normalized.Contains("چطور"))
        {
            return $"سوال خوبی پرسیدید! متأسفانه الان جواب دقیقش رو ندارم، ولی می‌تونم سرچ کنم اگه بخواید.\n\nف کافیه بگید «سرچ کن» تا در اینترنت دنبال جواب بگردم.";
        }

        return $"متوجه شدم. اگه سوال دقیق‌تری دارید بفرمایید تا کمکتون کنم. همچنین می‌تونم در اینترنت سرچ کنم.";
    }
}

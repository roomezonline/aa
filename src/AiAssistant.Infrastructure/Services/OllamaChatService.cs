using AiAssistant.Core.Configuration;
using AiAssistant.Core.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AiAssistant.Infrastructure.Services;

public class OllamaChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _settings;
    private readonly IConversationManager _conversationManager;

    public OllamaChatService(
        HttpClient httpClient,
        IOptions<AiSettings> settings,
        IConversationManager conversationManager)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _conversationManager = conversationManager;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string userMessage,
        string conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = await _conversationManager.GetHistoryAsync(
            conversationId, _settings.MaxContextMessages);

        var messages = new List<object>
        {
            new { role = "system", content = _settings.SystemPrompt }
        };

        foreach (var msg in history)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }
        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = _settings.DefaultModel,
            messages = messages,
            stream = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullResponse = new StringBuilder();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;

            var text = ParseStreamLine(line);
            if (text != null)
            {
                fullResponse.Append(text);
                yield return text;
            }
        }

        await _conversationManager.AddMessageAsync(conversationId, "user", userMessage);
        await _conversationManager.AddMessageAsync(conversationId, "assistant", fullResponse.ToString());
    }

    private static string? ParseStreamLine(string line)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(line);
            if (json.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? "";
            }
        }
        catch { }
        return null;
    }
}

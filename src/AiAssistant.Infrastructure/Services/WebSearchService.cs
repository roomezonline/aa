using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiAssistant.Core.Interfaces;

namespace AiAssistant.Infrastructure.Services;

public class WebSearchService : ISearchService
{
    private readonly HttpClient _httpClient;

    public WebSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SearchResult>> SearchAsync(
        string query, 
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            return ParseSearchResults(html, maxResults);
        }
        catch
        {
            return [];
        }
    }

    private List<SearchResult> ParseSearchResults(string html, int maxResults)
    {
        var results = new List<SearchResult>();
        var links = ExtractBetween(html, "<a rel=\"nofollow\" class=\"result__a\" href=\"", "\"");
        var titles = ExtractBetween(html, "<a rel=\"nofollow\" class=\"result__a\" href=\"", "</a>");
        var snippets = ExtractBetween(html, "<a class=\"result__snippet\" href=\"", "</a>");

        for (int i = 0; i < Math.Min(maxResults, links.Count); i++)
        {
            var url = links.Count > i ? CleanUrl(links[i]) : "";
            var title = titles.Count > i ? CleanText(titles[i]) : "";
            var snippet = snippets.Count > i ? CleanText(snippets[i]) : "";

            if (!string.IsNullOrWhiteSpace(title))
            {
                results.Add(new SearchResult
                {
                    Title = title,
                    Url = url,
                    Snippet = snippet
                });
            }
        }

        return results;
    }

    private List<string> ExtractBetween(string source, string start, string end)
    {
        var results = new List<string>();
        int pos = 0;
        while (pos < source.Length)
        {
            int startIdx = source.IndexOf(start, pos, StringComparison.Ordinal);
            if (startIdx == -1) break;
            startIdx += start.Length;
            int endIdx = source.IndexOf(end, startIdx, StringComparison.Ordinal);
            if (endIdx == -1) break;
            results.Add(source[startIdx..endIdx]);
            pos = endIdx + end.Length;
        }
        return results;
    }

    private string CleanUrl(string url) => 
        url.Replace("%3A", ":").Replace("%2F", "/").Replace("%3F", "?").Replace("%3D", "=").Replace("%26", "&");

    private string CleanText(string text) =>
        System.Net.WebUtility.HtmlDecode(text)
            .Replace("<b>", "").Replace("</b>", "")
            .Replace("<em>", "").Replace("</em>", "")
            .Trim();
}

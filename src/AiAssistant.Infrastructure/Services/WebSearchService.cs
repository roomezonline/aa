using AiAssistant.Core.Interfaces;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace AiAssistant.Infrastructure.Services;

public class WebSearchService : ISearchService
{
    private readonly HttpClient _httpClient;

    public WebSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            return ParseSearchResults(html, maxResults);
        }
        catch
        {
            return new List<SearchResult>();
        }
    }

    private List<SearchResult> ParseSearchResults(string html, int maxResults)
    {
        var results = new List<SearchResult>();

        var linkPattern = @"<a rel=""nofollow"" class=""result__a"" href=""([^""]*)"">([^<]*)</a>";
        var snippetPattern = @"<a class=""result__snippet"" href=""[^""]*"">([^<]*(?:<[^>]*>[^<]*)*)</a>";

        var linkMatches = Regex.Matches(html, linkPattern, RegexOptions.Singleline);
        var snippetMatches = Regex.Matches(html, snippetPattern, RegexOptions.Singleline);

        for (int i = 0; i < Math.Min(maxResults, linkMatches.Count); i++)
        {
            var url = CleanUrl(linkMatches[i].Groups[1].Value);
            var title = CleanText(linkMatches[i].Groups[2].Value);
            var snippet = i < snippetMatches.Count
                ? CleanText(snippetMatches[i].Groups[1].Value)
                : "";

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

    private string CleanUrl(string url) =>
        url.Replace("%3A", ":").Replace("%2F", "/").Replace("%3F", "?")
            .Replace("%3D", "=").Replace("%26", "&").Replace("%25", "%");

    private string CleanText(string text) =>
        System.Net.WebUtility.HtmlDecode(text)
            .Replace("<b>", "").Replace("</b>", "")
            .Replace("<em>", "").Replace("</em>", "")
            .Replace("<strong>", "").Replace("</strong>", "")
            .Trim();
}

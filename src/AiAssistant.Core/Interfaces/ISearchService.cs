namespace AiAssistant.Core.Interfaces;

public interface ISearchService
{
    Task<List<SearchResult>> SearchAsync(
        string query, 
        int maxResults = 5,
        CancellationToken cancellationToken = default);
}

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

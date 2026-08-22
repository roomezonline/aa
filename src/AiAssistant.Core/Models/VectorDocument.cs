namespace AiAssistant.Core.Models;

public class VectorDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

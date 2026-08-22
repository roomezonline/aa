namespace AiAssistant.Core.Models;

public class KnowledgeEntry
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public float[]? Embedding { get; set; }
    public double Confidence { get; set; }
    public int UseCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

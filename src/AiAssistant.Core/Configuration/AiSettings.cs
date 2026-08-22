namespace AiAssistant.Core.Configuration;

public class AiSettings
{
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string DefaultModel { get; set; } = "llama3.2";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int MaxContextMessages { get; set; } = 20;
    public double SimilarityThreshold { get; set; } = 0.85;
    public string DatabasePath { get; set; } = "AiAssistant.db";
    public string SystemPrompt { get; set; } = "تو یک دستیار هوشمند فارسی هستی. پاسخ‌های دقیق، روان و مفید بده.";
}

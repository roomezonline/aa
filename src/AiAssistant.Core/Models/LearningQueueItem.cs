namespace AiAssistant.Core.Models;

public class LearningQueueItem
{
    public int Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int TimesFailed { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

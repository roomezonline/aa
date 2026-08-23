using AiAssistant.Core.Models;

namespace AiAssistant.Core.Interfaces;

public interface IEvolutionEngine
{
    Task<string> GetSmartResponseAsync(string userMessage, float[] embedding, CancellationToken ct = default);
    Task<List<LearningQueueItem>> GetPendingQueueAsync(CancellationToken ct = default);
    Task<int> GetQueueCountAsync(CancellationToken ct = default);
    Task StartEvolutionAsync(CancellationToken ct = default);
}

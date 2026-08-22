using AiAssistant.Core.Models;

namespace AiAssistant.Core.Interfaces;

public interface IKnowledgeBase
{
    Task<KnowledgeEntry?> FindBestMatchAsync(
        string question, 
        float[] questionEmbedding);
    Task SaveAsync(KnowledgeEntry entry);
    Task<List<KnowledgeEntry>> GetByTopicAsync(
        string topic, 
        int limit = 100);
    Task<List<KnowledgeEntry>> SearchAsync(
        string query, 
        float[] queryEmbedding, 
        int topK = 5);
    Task<int> GetTotalCountAsync();
}

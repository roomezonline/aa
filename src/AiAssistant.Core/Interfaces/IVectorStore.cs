using AiAssistant.Core.Models;

namespace AiAssistant.Core.Interfaces;

public interface IVectorStore
{
    Task SaveAsync(VectorDocument document);
    Task SaveBatchAsync(IEnumerable<VectorDocument> documents);
    Task<List<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding, 
        int topK = 5);
    Task<bool> DeleteAsync(string id);
}

namespace AiAssistant.Core.Interfaces;

public interface IEmbeddingService
{
    float[] GetEmbedding(string text);
    Task<float[]> GetEmbeddingAsync(
        string text, 
        CancellationToken cancellationToken = default);
}

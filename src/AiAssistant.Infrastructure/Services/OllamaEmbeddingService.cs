using AiAssistant.Core.Interfaces;
using System.Net.Http.Json;

namespace AiAssistant.Infrastructure.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;

    public OllamaEmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public float[] GetEmbedding(string text)
    {
        return GetEmbeddingAsync(text).GetAwaiter().GetResult();
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text, 
        CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model = "nomic-embed-text",
            input = text
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/embed", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(
            cancellationToken);

        return result?.Embeddings?.FirstOrDefault() ?? [];
    }

    private class EmbeddingResponse
    {
        public float[][]? Embeddings { get; set; }
    }
}

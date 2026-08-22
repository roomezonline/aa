using AiAssistant.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace AiAssistant.Infrastructure.Services;

public class SimpleEmbeddingService : IEmbeddingService
{
    private const int EmbeddingSize = 128;

    public float[] GetEmbedding(string text)
    {
        return GenerateEmbedding(text);
    }

    public Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GenerateEmbedding(text));
    }

    private static float[] GenerateEmbedding(string text)
    {
        var embedding = new float[EmbeddingSize];
        var normalized = text.ToLower().Normalize(NormalizationForm.FormC);

        var words = normalized.Split(
            new[] { ' ', ',', '.', '!', '?', '\n', '\t', '؛', '،', ':', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var hash = GetDeterministicHash(word);
            var index = Math.Abs(hash) % EmbeddingSize;
            embedding[index] += 1.0f;

            if (word.Length > 3)
            {
                var substringHash = GetDeterministicHash(word[..^1]);
                var subIndex = Math.Abs(substringHash) % EmbeddingSize;
                embedding[subIndex] += 0.5f;
            }
        }

        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return embedding;
    }

    private static int GetDeterministicHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        return BitConverter.ToInt32(bytes, 0);
    }
}

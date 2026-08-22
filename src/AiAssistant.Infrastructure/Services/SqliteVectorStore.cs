using AiAssistant.Core.Interfaces;
using AiAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace AiAssistant.Infrastructure.Services;

public class SqliteVectorStore : IVectorStore
{
    private readonly string _dbPath;

    public SqliteVectorStore(string dbPath = "AiAssistant.db")
    {
        _dbPath = dbPath;
    }

    public async Task SaveAsync(VectorDocument document)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO VectorDocuments (Id, Content, Embedding, Metadata)
            VALUES (@id, @content, @embedding, @metadata)";

        command.Parameters.AddWithValue("@id", document.Id);
        command.Parameters.AddWithValue("@content", document.Content);
        command.Parameters.AddWithValue("@embedding", SerializeFloats(document.Embedding));
        command.Parameters.AddWithValue("@metadata", System.Text.Json.JsonSerializer.Serialize(document.Metadata));

        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveBatchAsync(IEnumerable<VectorDocument> documents)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            foreach (var doc in documents)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO VectorDocuments (Id, Content, Embedding, Metadata)
                    VALUES (@id, @content, @embedding, @metadata)";

                command.Parameters.AddWithValue("@id", doc.Id);
                command.Parameters.AddWithValue("@content", doc.Content);
                command.Parameters.AddWithValue("@embedding", SerializeFloats(doc.Embedding));
                command.Parameters.AddWithValue("@metadata", System.Text.Json.JsonSerializer.Serialize(doc.Metadata));

                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 5)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await EnsureTableExistsAsync(connection);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Content, Embedding, Metadata
            FROM VectorDocuments";

        var results = new List<VectorSearchResult>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var content = reader.GetString(1);
            var embedding = DeserializeFloats(reader.GetString(2));
            var metadata = reader.GetString(3);

            var similarity = CosineSimilarity(queryEmbedding, embedding);

            results.Add(new VectorSearchResult
            {
                Id = id,
                Content = content,
                Score = similarity,
                Metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(metadata) ?? new()
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    public async Task<bool> DeleteAsync(string id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM VectorDocuments WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    private async Task EnsureTableExistsAsync(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS VectorDocuments (
                Id TEXT PRIMARY KEY,
                Content TEXT NOT NULL,
                Embedding TEXT NOT NULL,
                Metadata TEXT
            )";
        await command.ExecuteNonQueryAsync();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-10);
    }

    private static string SerializeFloats(float[] floats) =>
        string.Join(",", floats.Select(f => f.ToString("F6")));

    private static float[] DeserializeFloats(string serialized) =>
        serialized.Split(',').Select(float.Parse).ToArray();
}

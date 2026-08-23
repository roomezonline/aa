using AiAssistant.Core.Interfaces;
using AiAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiAssistant.Infrastructure.Services;

public class EvolutionBackgroundService : BackgroundService
{
    private readonly ILogger<EvolutionBackgroundService> _logger;
    private readonly string _connectionString;

    public EvolutionBackgroundService(
        ILogger<EvolutionBackgroundService> logger,
        string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Evolution Engine started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var searchService = CreateSearchService();
                var embeddingService = new SimpleEmbeddingService();
                var knowledgeService = new KnowledgeService(
                    CreateContext, new SqliteVectorStore(_connectionString), embeddingService);
                var evolution = new EvolutionEngine(searchService, knowledgeService, embeddingService, CreateContext);

                _logger.LogInformation("Evolution learning cycle started");
                await evolution.StartEvolutionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evolution learning cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Evolution Engine stopped");
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    private WebSearchService CreateSearchService()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return new WebSearchService(client);
    }
}

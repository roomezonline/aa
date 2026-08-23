using AiAssistant.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiAssistant.Infrastructure.Services;

public class EvolutionBackgroundService : BackgroundService
{
    private readonly ILogger<EvolutionBackgroundService> _logger;
    private readonly Func<IEvolutionEngine> _evolutionFactory;

    public EvolutionBackgroundService(
        ILogger<EvolutionBackgroundService> logger,
        Func<IEvolutionEngine> evolutionFactory)
    {
        _logger = logger;
        _evolutionFactory = evolutionFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Evolution Engine started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var evolution = _evolutionFactory();
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
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Evolution Engine stopped");
    }
}

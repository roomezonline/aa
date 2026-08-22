namespace AiAssistant.Core.Interfaces;

public interface ISelfLearner
{
    Task StartLearningAsync(
        string topic, 
        TimeSpan duration,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

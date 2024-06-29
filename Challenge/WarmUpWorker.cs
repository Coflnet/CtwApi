public class WarmUpWorker : BackgroundService
{
    public WarmUpWorker(StreakService streakService, ILogger<WarmUpWorker> logger)
    {
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // just create the injected services
        return Task.CompletedTask;
    }
}
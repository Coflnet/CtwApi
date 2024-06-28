public class DeletorService : BackgroundService
{
    private readonly IServiceProvider services;

    public DeletorService(IServiceProvider services)
    {
        this.services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = services.CreateScope();
            var statsService = scope.ServiceProvider.GetRequiredService<StatsService>();
            await statsService.DeleteOldStats();
            await Task.Delay(TimeSpan.FromHours(2), stoppingToken);
        }
    }
}
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Microsoft.AspNetCore.StaticFiles;
using ISession = Cassandra.ISession;

public class StatsService
{
    private readonly Table<Stat> statsService;
    private readonly Table<TimedStat> timedStatTable;

    public StatsService(ISession session)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<Stat>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.StatName)
            .Column(t => t.Value, cm => cm.AsCounter())
        );
        statsService = new Table<Stat>(session, mapping, "stats");
        statsService.CreateIfNotExists();

        var timedMapping = new MappingConfiguration()
            .Define(new Map<TimedStat>()
            .PartitionKey(t => t.ExpiresOnDay)
            .ClusteringKey(t => t.StatName)
            .ClusteringKey(t => t.UserId)
            .Column(t => t.Value, cm => cm.AsCounter())
        );
        timedStatTable = new Table<TimedStat>(session, timedMapping, "timed_stats");
        timedStatTable.CreateIfNotExists();
    }

    public async Task IncreaseStat(Guid userId, string statName, long value = 1)
    {
        await statsService.Where(s => s.UserId == userId && s.StatName == statName)
            .Select(s => new Stat() { Value = value })
            .Update().ExecuteAsync();
    }

    public async Task<long> GetStat(Guid userId, string statName)
    {
        var stat = await statsService.Where(s => s.UserId == userId && s.StatName == statName)
            .Select(s => new { s.Value })
            .ExecuteAsync();
        return stat.FirstOrDefault()?.Value ?? 0;
    }

    public async Task IncreaseExpireStat(DateTimeOffset time, Guid userId, string statName, long value = 1)
    {
        int dayTodelete = GetTimeKey(time);
        await timedStatTable.Where(s => s.UserId == userId && s.StatName == statName && s.ExpiresOnDay == dayTodelete)
            .Select(s => new TimedStat() { Value = value })
            .Update().ExecuteAsync();
    }

    public async Task<long> GetExpireStat(DateTimeOffset time, Guid userId, string statName)
    {
        int expiresAtHour = GetTimeKey(time);
        var stat = await timedStatTable.Where(s => s.UserId == userId && s.StatName == statName && s.ExpiresOnDay == expiresAtHour)
            .Select(s => new { s.Value })
            .ExecuteAsync();
        return stat.FirstOrDefault()?.Value ?? 0;
    }

    private static int GetTimeKey(DateTimeOffset time)
    {
        return (int)(time - new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalDays;
    }

    public async Task<IEnumerable<Stat>> GetStats(Guid userId)
    {
        return await statsService.Where(s => s.UserId == userId).ExecuteAsync();
    }

    public async Task DeleteOldStats()
    {
        var yesterday = GetTimeKey(DateTimeOffset.UtcNow.AddDays(-1));
        await timedStatTable.Where(s => s.ExpiresOnDay == yesterday).Delete().ExecuteAsync();
    }

    internal async Task AddExp(Guid userId, int reward)
    {
        await IncreaseStat(userId, "exp", reward);
    }
}

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
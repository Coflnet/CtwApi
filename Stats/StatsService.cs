using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Microsoft.AspNetCore.StaticFiles;
using ISession = Cassandra.ISession;

public class StatsService
{
    private readonly Table<Stat> statsTable;
    private readonly Table<TimedStat> timedStatTable;
    private readonly ILogger<StatsService> logger;
    private readonly LeaderboardService leaderboardService;
    public StatsService(ISession session, ILogger<StatsService> logger, LeaderboardService leaderboardService)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<Stat>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.StatName)
            .Column(t => t.Value, cm => cm.AsCounter())
        );
        statsTable = new Table<Stat>(session, mapping, "stats");
        statsTable.CreateIfNotExists();

        var timedMapping = new MappingConfiguration()
            .Define(new Map<TimedStat>()
            .PartitionKey(t => t.ExpiresOnDay)
            .ClusteringKey(t => t.StatName)
            .ClusteringKey(t => t.UserId)
            .Column(t => t.Value, cm => cm.AsCounter())
        );
        timedStatTable = new Table<TimedStat>(session, timedMapping, "timed_stats");
        timedStatTable.CreateIfNotExists();
        this.logger = logger;
        this.leaderboardService = leaderboardService;
    }

    public async Task IncreaseStat(Guid userId, string statName, long value = 1)
    {
        logger.LogInformation($"Increasing {statName} for {userId} by {value}");
        await statsTable.Where(s => s.UserId == userId && s.StatName == statName)
            .Select(s => new Stat() { Value = value })
            .Update().ExecuteAsync();
    }

    public async Task<long> GetStat(Guid userId, string statName)
    {
        var stat = await statsTable.Where(s => s.UserId == userId && s.StatName == statName)
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
        return await statsTable.Where(s => s.UserId == userId).ExecuteAsync();
    }

    public async Task DeleteOldStats()
    {
        var yesterday = GetTimeKey(DateTimeOffset.UtcNow.AddDays(-1));
        await timedStatTable.Where(s => s.ExpiresOnDay == yesterday).Delete().ExecuteAsync();
    }

    internal async Task AddExp(Guid userId, long reward)
    {
        await IncreaseStat(userId, "exp", reward);
        var currentExp = await GetStat(userId, "exp");
        await leaderboardService.SetScore(new BoardNames().Exp, userId, currentExp);
    }

    internal async Task DeleteStats(Guid userId)
    {
        await statsTable.Where(s => s.UserId == userId).Delete().ExecuteAsync();
    }
}

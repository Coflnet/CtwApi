using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Microsoft.AspNetCore.StaticFiles;
using ISession = Cassandra.ISession;

public class StatsService
{
    private readonly Table<Stat> statsService;

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

    public async Task<IEnumerable<Stat>> GetStats(Guid userId)
    {
        return await statsService.Where(s => s.UserId == userId).ExecuteAsync();
    }
}

using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

public class SkipService
{
    public Table<SkipEntry> skipTable;
    private StatsService statsService;

    public SkipService(ISession session, StatsService statsService)
    {
        this.statsService = statsService;
        var mapping = new MappingConfiguration()
            .Define(new Map<SkipEntry>()
            .PartitionKey(s => s.UserId)
            .ClusteringKey(s => s.Day)
            .Column(s => s.Type)
            .Column(s => s.Label)
        );
        skipTable = new Table<SkipEntry>(session, mapping, "skip_entries");
        skipTable.CreateIfNotExists();
    }

    public class SkipEntry
    {
        public Guid UserId { get; set; }
        public int Day { get; set; }
        public string Type { get; set; }
        public string Label { get; set; }
    }

    /// <summary>
    /// Tries to skip the current task for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="label"></param>
    /// <returns></returns>
    public async Task<bool> TrySkip(Guid userId, string label)
    {
        var day = DateTime.UtcNow.DayOfYear;
        (int used, int collected) = await SkipStat(userId);
        if (used - collected > 2)
        {
            return false;
        }
        var insert = skipTable.Insert(new SkipEntry() { UserId = userId, Day = day, Label = label, Type = "skip" });
        insert.SetTTL(86400);
        var task = statsService.IncreaseStat(userId, "current_offset");
        await insert.ExecuteAsync();
        await task;
        return true;
    }

    public async Task<(int used, int collected)> SkipStat(Guid userId)
    {
        var day = DateTime.UtcNow.DayOfYear;
        var entries = (await skipTable.Where(s => s.UserId == userId && s.Day == day).ExecuteAsync()).ToList();
        var used = entries.Count(e => e.Type == "skip");
        var collected = entries.Count(e => e.Type == "collect");
        return (used, collected);
    }

    public async Task<SkipsAvailable> Available(Guid userId)
    {
        var (used, collected) = await SkipStat(userId);
        return new SkipsAvailable() { Used = used, Total = 2 - used + collected };
    }

    public async Task Collected(Guid userId, string label)
    {
        var day = DateTime.UtcNow.DayOfYear;
        var insert = skipTable.Insert(new SkipEntry() { UserId = userId, Day = day, Label = label, Type = "collect" });
        insert.SetTTL(86400);
        await insert.ExecuteAsync();
    }
}

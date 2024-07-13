using System.ComponentModel;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

public class EventStorageService
{
    private readonly StatsService statsService;
    private readonly Table<ChangeEvent> expChangeTable;
    private readonly ILogger<EventStorageService> logger;

    public EventStorageService(ISession session, StatsService statsService, ILogger<EventStorageService> logger)
    {
        this.statsService = statsService;
        this.logger = logger;
        var mapping = new MappingConfiguration()
            .Define(new Map<ChangeEvent>()
            .PartitionKey(e => e.UserId)
            .ClusteringKey(e => e.Time, SortOrder.Descending)
            .Column(e => e.Type, cm=>cm.WithDbType<int>())
        );
        expChangeTable = new Table<ChangeEvent>(session, mapping, "change_events");
        expChangeTable.CreateIfNotExists();
    }

    public async Task AddExp(Guid userId, long reward, string source, string description, string reference)
    {
        var insert = expChangeTable.Insert(new ChangeEvent()
        {
            UserId = userId,
            Time = DateTimeOffset.UtcNow,
            Change = reward,
            Source = source,
            Description = description,
            Reference = reference,
            Type = ChangeEvent.ChangeType.Exp
        });
        insert.SetTTL(86400 * 30);
        await insert.ExecuteAsync();
        await statsService.AddExp(userId, reward);
    }

    public async Task AddSkip(Guid userId, string source, string description, string reference)
    {
        var insert = expChangeTable.Insert(new ChangeEvent()
        {
            UserId = userId,
            Time = DateTimeOffset.UtcNow,
            Change = 0,
            Source = source,
            Description = description,
            Reference = reference,
            Type = ChangeEvent.ChangeType.Skip
        });
        insert.SetTTL(86400 * 30);
        await insert.ExecuteAsync();
    }

    public async Task<IEnumerable<ChangeEvent>> GetChanges(Guid userId, DateTime since)
    {
        return await expChangeTable.Where(e => e.UserId == userId && e.Time > since).ExecuteAsync();
    }
}

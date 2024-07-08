using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

public class ExpService
{
    private readonly StatsService statsService;
    private readonly Table<ExpChange> expChangeTable;
    private readonly ILogger<ExpService> logger;

    public ExpService(ISession session, StatsService statsService, ILogger<ExpService> logger)
    {
        this.statsService = statsService;
        this.logger = logger;
        var mapping = new MappingConfiguration()
            .Define(new Map<ExpChange>()
            .PartitionKey(e => e.UserId)
            .ClusteringKey(e => e.Time, SortOrder.Descending)
        );
        expChangeTable = new Table<ExpChange>(session, mapping, "exp_changes");
        expChangeTable.CreateIfNotExists();
    }

    public async Task AddExp(Guid userId, long reward, string source, string description, string reference)
    {
        var insert = expChangeTable.Insert(new ExpChange()
        {
            UserId = userId,
            Time = DateTimeOffset.UtcNow,
            Change = reward,
            Source = source,
            Description = description,
            Reference = reference
        });
        insert.SetTTL(86400 * 30);
        await insert.ExecuteAsync();
        await statsService.AddExp(userId, reward);
    }

    public async Task<IEnumerable<ExpChange>> GetExpChanges(Guid userId)
    {
        return await expChangeTable.Where(e => e.UserId == userId).ExecuteAsync();
    }
}

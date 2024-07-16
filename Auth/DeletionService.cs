using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Auth;

public class DeletionService
{
    private readonly Table<InternalDeletionRequest> deletionDb;
    private readonly LeaderboardService leaderboardService;
    private readonly AuthService authService;
    private readonly StatsService statsService;

    public DeletionService(ISession session, LeaderboardService leaderboardService, AuthService authService, StatsService statsService)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<InternalDeletionRequest>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.RequestedAt, SortOrder.Descending)
            .Column(t => t.RequestedAt)
            .Column(t => t.DeletedAt)
        );
        deletionDb = new Table<InternalDeletionRequest>(session, mapping, "deletion_requests");
        deletionDb.CreateIfNotExists();
        this.leaderboardService = leaderboardService;
        this.authService = authService;
        this.statsService = statsService;
    }

    public async Task<DateTimeOffset> RequestDeletion(Guid userId)
    {
        var deletionRequest = new InternalDeletionRequest()
        {
            UserId = userId,
            RequestedAt = DateTimeOffset.UtcNow,
        };
        await deletionDb.Insert(deletionRequest).ExecuteAsync();
        return deletionRequest.RequestedAt + TimeSpan.FromDays(3);
    }

    public async Task<DateTimeOffset?> DeletingAt(Guid userId)
    {
        var deletionRequest = await deletionDb.Where(d => d.UserId == userId).FirstOrDefault().ExecuteAsync();
        return deletionRequest?.RequestedAt + TimeSpan.FromDays(3);
    }

    public async Task RunDeletions()
    {
        var deletionRequests = await deletionDb.Where(d => d.DeletedAt == null).ExecuteAsync();
        foreach (var request in deletionRequests)
        {
            if (request.RequestedAt.AddDays(3) >= DateTimeOffset.UtcNow)
            {
                continue;
            }
            await leaderboardService.DeleteProfile(request.UserId);
            await authService.DeleteUser(request.UserId);
            await statsService.DeleteStats(request.UserId);
            request.DeletedAt = DateTimeOffset.UtcNow;
            deletionDb.Insert(request).Execute();
        }

    }

    public class InternalDeletionRequest
    {
        public Guid UserId { get; set; }
        public DateTimeOffset RequestedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
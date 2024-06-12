using Cassandra.Data.Linq;
using Cassandra.Mapping;

public class ChallengeService
{

    private readonly EventBusService eventBus;
    private Table<Challenge> challengeTable;
    private readonly ILogger<ChallengeService> logger;
    private readonly StatsService statsService;

    public ChallengeService(EventBusService eventBus, Cassandra.ISession session, ILogger<ChallengeService> logger, StatsService statsService)
    {
        this.eventBus = eventBus;
        eventBus.ImageUploaded += (sender, e) =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await HandOutReward(e);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to hand out reward");
                }
            });
        };

        var mapping = new MappingConfiguration()
            .Define(new Map<Challenge>()
            .PartitionKey(t => t.UserId, t => t.Date)
            .ClusteringKey(t => t.Type)
        );
        challengeTable = new Table<Challenge>(session, mapping, "challenges");
        challengeTable.CreateIfNotExists();
        this.logger = logger;
        this.statsService = statsService;
    }

    private async Task HandOutReward(ImageUploadEvent e)
    {
        var challenges = challengeTable.Where(c => c.UserId == e.UserId && c.Date == DateTime.Today).Execute();
        foreach (var item in challenges)
        {
            if (item.Type == "count")
            {
                item.Progress++;
            }
            else if (item.Type == "exp")
            {
                item.Progress += e.Exp;
            }
            else
                continue;

            if (item.Progress >= item.Target && !item.RewardPaid)
            {
                await statsService.AddExp(e.UserId, item.Reward);
                item.RewardPaid = true;
            }

            await AddOrUpdateChallenge(item);
        }
    }

    internal async Task<ChallengeController.ChallengeResponse> GetDailyChallenges(Guid userId)
    {
        var challenges = (await challengeTable.Where(c => c.UserId == userId && c.Date == DateTime.Today).ExecuteAsync()).ToArray();
        if (challenges.Any())
        {
            return new ChallengeController.ChallengeResponse()
            {
                Success = true,
                Challenges = challenges
            };
        }
        // create new challenges
        var newChallenges = new List<Challenge>
        {
            new Challenge()
            {
                UserId = userId,
                Date = DateTime.Today,
                Type = "count",
                Progress = 0,
                Target = 7,
                Reward = 500
            }
        };
        foreach (var challenge in newChallenges)
        {
            await AddOrUpdateChallenge(challenge);
        }
        return new ChallengeController.ChallengeResponse()
        {
            Success = true,
            Challenges = newChallenges.ToArray()
        };
    }

    private async Task AddOrUpdateChallenge(Challenge challenge)
    {
        var insert = challengeTable.Insert(challenge);
        insert.SetTTL(86400 * 5);
        await insert.ExecuteAsync();
    }
}

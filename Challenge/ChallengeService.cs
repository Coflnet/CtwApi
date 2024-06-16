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
            .ClusteringKey(t => t.RewardPaid)
            .ClusteringKey(t => t.Type)
        );
        challengeTable = new Table<Challenge>(session, mapping, "challenges_2");
        challengeTable.CreateIfNotExists();
        this.logger = logger;
        this.statsService = statsService;
    }

    private async Task HandOutReward(ImageUploadEvent e)
    {
        var dates = new DateTime[] { DateTime.Today, default };
        var challenges = challengeTable.Where(c => c.UserId == e.UserId && dates.Contains(c.Date) && !c.RewardPaid).Execute();
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
            else if (item.Type == "unique" && e.IsUnique)
            {
                item.Progress++;
            }
            else if (item.Type == "new" && e.Exp >= 20)
            {
                item.Progress++;
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

    public async Task<ChallengeController.ChallengeResponse> GetLongTermChallenges(Guid userId)
    {
        var challenges = (await challengeTable.Where(c => c.UserId == userId && c.Date == default).ExecuteAsync()).ToArray();
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
                Date = default,
                Type = "unique",
                Progress = 0,
                Target = 20,
                Reward = 5000
            },
            new Challenge()
            {
                UserId = userId,
                Date = default,
                Type = "new",
                Progress = 0,
                Target = 5,
                Reward = 4000
            },
            new Challenge()
            {
                UserId = userId,
                Date = default,
                Type = "new",
                Progress = 0,
                Target = 50,
                Reward = 50000
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
        if (challenge.Date != default)
            insert.SetTTL(86400 * 5);
        await insert.ExecuteAsync();
    }
}

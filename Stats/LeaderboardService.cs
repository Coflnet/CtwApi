using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Leaderboard.Client.Api;
using Coflnet.Leaderboard.Client.Model;
using ISession = Cassandra.ISession;

public class LeaderboardService
{
    IScoresApi scoresApi;
    Table<Profile> profileTable;
    string prefix = "ctw_";
    ILogger<LeaderboardService> logger;

    public LeaderboardService(IScoresApi scoresApi, ISession session, ILogger<LeaderboardService> logger)
    {
        this.scoresApi = scoresApi;
        var mapping = new MappingConfiguration()
            .Define(new Map<Profile>()
            .PartitionKey(t => t.UserId)
            .Column(t => t.Name)
            .Column(t => t.Avatar)
        );
        profileTable = new Table<Profile>(session, mapping, "leaderboard_profiles");
        profileTable.CreateIfNotExists();
        this.logger = logger;
    }

    public class Profile
    {
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string? Avatar { get; set; }
    }

    public class PublicProfile
    {
        public string Name { get; set; }
        public string Avatar { get; set; }
    }

    public class BoardEntry
    {
        public Profile? User { get; set; }
        public long Score { get; set; }
    }

    public async Task<IEnumerable<BoardEntry>> GetLeaderboard(string leaderboardId, int offset = 0, int count = 10)
    {
        var users = await scoresApi.ScoresLeaderboardSlugGetAsync(GetPrfixed(leaderboardId), offset, count);
        return await FormatWithProfile(users);
    }

    private string GetPrfixed(string leaderboardId)
    {
        var boardNames = new BoardNames();
        var converted = leaderboardId switch {
            "daily" => boardNames.DailyExp,
            "weekly" => boardNames.WeeklyExp,
            "overall" => boardNames.Exp,
            _ => leaderboardId
        };
        return prefix + converted;
    }

    private async Task<IEnumerable<BoardEntry>> FormatWithProfile(List<BoardScore> users)
    {
        var ids = users.Select(u => Guid.Parse(u.UserId)).ToHashSet();
        var profiles = (await profileTable.Where(p => ids.Contains(p.UserId)).ExecuteAsync()).ToHashSet();
        logger.LogInformation($"Found {profiles.Count()} profiles for {ids.Count()} users last one {profiles.LastOrDefault()?.Name}");
        return users.Select(u => new BoardEntry()
        {
            User = profiles.FirstOrDefault(p => p.UserId == Guid.Parse(u.UserId)) ?? new Profile() { Name = "Anonymous", Avatar = null, UserId = Guid.Parse(u.UserId) },
            Score = u.Score
        });
    }

    public async Task<IEnumerable<BoardEntry>> GetLeaderboardAroundMe(string leaderboardId, Guid userId, int count = 10)
    {
        var around = await scoresApi.ScoresLeaderboardSlugUserUserIdGetAsync(GetPrfixed(leaderboardId), userId.ToString(), count, count / 2);
        return await FormatWithProfile(around);
    }

    public async Task<PublicProfile> GetProfile(Guid userId)
    {
        var internalProfile = await profileTable.Where(p => p.UserId == userId).FirstOrDefault().ExecuteAsync();
        return new PublicProfile() { Name = internalProfile?.Name ?? "Anonymous", Avatar = internalProfile?.Avatar ?? "" };
    }

    public async Task SetProfile(Guid userId, string name, string avatar)
    {
        await profileTable.Insert(new Profile() { UserId = userId, Name = name, Avatar = avatar }).ExecuteAsync();
        logger.LogInformation($"Set profile for {userId} to {name} with avatar {avatar}");
    }

    public async Task SetScore(string leaderboardId, Guid userId, long score)
    {
        await scoresApi.ScoresLeaderboardSlugPostAsync(GetPrfixed(leaderboardId), new()
        {
            Confidence = 1,
            Score = score,
            HighScore = true,
            UserId = userId.ToString(),
            DaysToKeep = 30
        });
    }

    public async Task<long> GetRankOf(string leaderboardId, Guid userId)
    {
        return await scoresApi.ScoresLeaderboardSlugUserUserIdRankGetAsync(GetPrfixed(leaderboardId), userId.ToString());
    }

    internal async Task<RankSummary> GetRanks(Guid guid)
    {
        var names = new BoardNames();
        var dailyRankTask = GetRankOf(names.DailyExp, guid);
        var weeklyRankTask = GetRankOf(names.WeeklyExp, guid);
        var overallRankTask = GetRankOf(names.Exp, guid);
        return new RankSummary()
        {
            DailyRank = await dailyRankTask,
            WeeklyRank = await weeklyRankTask,
            OverallRank = await overallRankTask
        };
    }
}

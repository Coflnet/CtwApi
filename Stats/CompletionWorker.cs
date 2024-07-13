using Coflnet.Auth;
/// <summary>
/// Checks the top 10 on the leaderboard and gives them a reward
/// </summary>
public class CompletionWorker : BackgroundService
{
    LeaderboardService leaderboardService;
    StatsService statsService;
    StreakService streakService;
    ILogger<CompletionWorker> logger;
    RewardsConfig rewardsConfig;
    EventStorageService expService;

    public CompletionWorker(LeaderboardService leaderboardService, StatsService statsService, ILogger<CompletionWorker> logger, StreakService streakService, RewardsConfig rewardsConfig, EventStorageService expService)
    {
        this.leaderboardService = leaderboardService;
        this.statsService = statsService;
        this.logger = logger;
        this.streakService = streakService;
        this.rewardsConfig = rewardsConfig;
        this.expService = expService;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var boardNames = new BoardNames();
            var tillEnd = DateTime.Now.RoundDown(TimeSpan.FromDays(1)).AddDays(1) - DateTime.Now;
            if (tillEnd.TotalMilliseconds > 0)
            {
                logger.LogInformation($"Waiting for {tillEnd}");
                await Task.Delay(tillEnd, stoppingToken);
            }
            logger.LogInformation("Checking leaderboard top 10");
            var topUsers = await leaderboardService.GetLeaderboard(boardNames.DailyExp, 0, rewardsConfig.DailyLeaderboard.GivenTo);
            var offset = 0;
            foreach (var entry in topUsers)
            {
                if (entry.User != null)
                    await statsService.IncreaseStat(entry.User.UserId, "daily_leaderboard_top10", 1);
                logger.LogInformation("Increased daily_leaderboard_top10 for {userId}", entry.User?.UserId);
                var bonus = GetBonus(rewardsConfig.DailyLeaderboard, offset++);
                if (entry.User != null)
                    await expService.AddExp(entry.User.UserId, bonus, "leaderboard", $"Placing #{offset} on the daily leaderboard", boardNames.DailyExp);
            }
            var newBoardNames = new BoardNames();
            if (newBoardNames.WeeklyExp != boardNames.WeeklyExp)
            {
                await UpdateWeeklyBoard(boardNames, topUsers, offset);
            }

            await streakService.UpdateStatifStreakBroken();
        }
    }

    private static int GetBonus(LeaderboardRewards dailyLeaderboard, int offset)
    {
        if (offset >= dailyLeaderboard.GivenTo)
            return 0;
        var extra = offset switch
        {
            0 => dailyLeaderboard.First,
            1 => dailyLeaderboard.Second,
            2 => dailyLeaderboard.Third,
            _ => 0
        };
        return extra + dailyLeaderboard.RewardAmount;
    }

    private async Task<int> UpdateWeeklyBoard(BoardNames boardNames, IEnumerable<LeaderboardService.BoardEntry> top10, int offset)
    {
        var top20 = await leaderboardService.GetLeaderboard(boardNames.WeeklyExp, 0, 10);
        foreach (var entry in top10.Take(10))
        {
            if (entry.User != null)
                await statsService.IncreaseStat(entry.User.UserId, "weekly_leaderboard_top10", 1);
            logger.LogInformation("Increased weekly_leaderboard_top10 for {userId}", entry.User?.UserId);
        }
        foreach (var item in top20)
        {
            var bonus = GetBonus(rewardsConfig.WeeklyLeaderboard, offset++);
            if (item.User != null)
                await expService.AddExp(item.User.UserId, bonus, "leaderboard", $"Placing #{offset} on the weekly leaderboard", boardNames.WeeklyExp);
        }

        return offset;
    }
}

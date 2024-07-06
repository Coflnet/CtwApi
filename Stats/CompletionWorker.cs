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

    public CompletionWorker(LeaderboardService leaderboardService, StatsService statsService, ILogger<CompletionWorker> logger, StreakService streakService)
    {
        this.leaderboardService = leaderboardService;
        this.statsService = statsService;
        this.logger = logger;
        this.streakService = streakService;
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
            var top10 = await leaderboardService.GetLeaderboard(boardNames.DailyExp, 0, 10);
            var offset = 0;
            foreach (var entry in top10)
            {
                if (entry.User != null)
                    await statsService.IncreaseStat(entry.User.UserId, "daily_leaderboard_top10", 1);
                logger.LogInformation("Increased daily_leaderboard_top10 for {userId}", entry.User?.UserId);
                var bonus = Math.Max(3 - offset++, 0) * 1000 + 1000;
                if (entry.User != null)
                    await statsService.AddExp(entry.User.UserId, bonus);
            }
            var newBoardNames = new BoardNames();
            if (newBoardNames.WeeklyExp != boardNames.WeeklyExp)
            {
                await UpdateWeeklyBoard(boardNames, top10, offset);
            }

            await streakService.UpdateStatifStreakBroken();
        }
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
            var bonus = Math.Max(3 - offset++, 0) * 2000 + 3000;
            if (item.User != null)
                await statsService.AddExp(item.User.UserId, bonus);
        }

        return offset;
    }
}

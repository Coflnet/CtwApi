using Coflnet.Auth;
/// <summary>
/// Checks the top 10 on the leaderboard and gives them a reward
/// </summary>
public class CompletionWorker : BackgroundService
{
    LeaderboardService leaderboardService;
    StatsService statsService;
    ILogger<CompletionWorker> logger;

    public CompletionWorker(LeaderboardService leaderboardService, StatsService statsService, ILogger<CompletionWorker> logger)
    {
        this.leaderboardService = leaderboardService;
        this.statsService = statsService;
        this.logger = logger;
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
            foreach (var entry in top10)
            {
                if (entry.User != null)
                    await statsService.IncreaseStat(entry.User.UserId, "daily_leaderboard_top10", 1);
                logger.LogInformation($"Increased daily_leaderboard_top10 for {entry.User?.UserId}");
            }
            var newBoardNames = new BoardNames();
            if (newBoardNames.WeeklyExp != boardNames.WeeklyExp)
            {
                top10 = await leaderboardService.GetLeaderboard(boardNames.WeeklyExp, 0, 10);
                foreach (var entry in top10)
                {
                    if (entry.User != null)
                        await statsService.IncreaseStat(entry.User.UserId, "weekly_leaderboard_top10", 1);
                    logger.LogInformation($"Increased weekly_leaderboard_top10 for {entry.User?.UserId}");
                }
            }

        }
    }
}

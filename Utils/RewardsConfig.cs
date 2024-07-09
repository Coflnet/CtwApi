public class RewardsConfig
{
    /// <summary>
    /// Rewards for placing on the top of the weekly leaderboard
    /// </summary>
    public LeaderboardRewards WeeklyLeaderboard { get; set; } = new();
    /// <summary>
    /// Rewards for placing on the top of the daily leaderboard
    /// </summary>
    public LeaderboardRewards DailyLeaderboard { get; set; } = new();
    /// <summary>
    /// Multiplier Tiers
    /// </summary>
    public MultiplierRewards MultiplierRewards { get; set; } = new();
}

public class MultiplierRewards
{
    public float Top { get; set; }
    public float Second { get; set; }
    public float Third { get; set; }
}

public class LeaderboardRewards
{
    public int RewardAmount { get; set; }
    public int GivenTo { get; set; }
    public int First { get; set; }
    public int Second { get; set; }
    public int Third { get; set; }
}

using Coflnet.Auth;

public class BoardNames
{
    public string Exp { get; set; } = "exp_overall";
    public string WeeklyExp { get; set; } = "exp_weekly_" + DateTime.Now.RoundDown(TimeSpan.FromDays(7)).AddDays(7).ToString("yyyyMMdd");
    public string DailyExp { get; set; } = "exp_daily_" + DateTime.Now.ToString("yyyyMMdd");
}
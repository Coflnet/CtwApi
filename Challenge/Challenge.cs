public class Challenge
{
    public Guid UserId { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; }
    public int Progress { get; set; }
    public int Target { get; set; }
    public int Reward { get; set; }
    public bool RewardPaid { get; set; }
}

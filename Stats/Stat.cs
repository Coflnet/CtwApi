public class Stat
{
    public Guid UserId { get; set; }
    public string StatName { get; set; }
    public long Value { get; set; }
}

public class TimedStat : Stat
{
    public int ExpiresOnDay { get; set; }
}
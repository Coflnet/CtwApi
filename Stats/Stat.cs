public class Stat
{
    public Guid UserId { get; set; }
    public string StatName { get; set; }
    public long Value { get; set; }

    public Stat()
    {
        UserId = Guid.Empty;
        StatName = "";
        Value = 0;
    }

    public Stat(Guid userId, string statName, long value)
    {
        UserId = userId;
        StatName = statName;
        Value = value;
    }
}

public class TimedStat : Stat
{
    public int ExpiresOnDay { get; set; }
}
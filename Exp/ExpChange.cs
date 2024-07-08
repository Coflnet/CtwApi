public class ExpChange
{
    public Guid UserId { get; set; }
    public DateTimeOffset Time { get; set; }
    public long Change { get; set; }
    public string Source { get; set; }
    public string Reference { get; set; }
    public string Description { get; set; }

}
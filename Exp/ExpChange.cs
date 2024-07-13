public class ChangeEvent
{
    public Guid UserId { get; set; }
    public DateTimeOffset Time { get; set; }
    public long Change { get; set; }
    public string Source { get; set; }
    public string Reference { get; set; }
    public string Description { get; set; }
    public ChangeType Type { get; set; }
    
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum ChangeType 
    {
        Unknown = 0,
        Exp = 1,
        Skip = 2
    }
}
public class UploadRewards
{
    public long Total { get; set; }
    public float Multiplier { get; set; }
    public long ImageBonus { get; set; }
    public bool IsCurrent { get; set; }
    /// <summary>
    /// Has never been uploaded before
    /// </summary>
    public bool Unique { get; set; }
}

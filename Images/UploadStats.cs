public class UploadStats
{
    public bool ExtendedStreak { get; set; }
    /// <summary>
    /// How many times the image was collected before
    /// </summary>
    public long CollectedTimes { get; set; }
    /// <summary>
    /// label is not able to be collected, maybe scrambled 
    /// </summary>
    public bool IsNoItem { get; set; }
}
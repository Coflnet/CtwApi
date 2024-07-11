public class UploadRewards
{
    public long Total { get; set; }
    public float Multiplier { get; set; }
    public long BaseReward { get; set; }
    public bool IsCurrent { get; set; }
    /// <summary>
    /// Rewarded a skip for the upload
    /// </summary>
    public bool AddedSkip { get; set; }
    /// <summary>
    /// Has never been uploaded before
    /// </summary>
    public bool Unique { get; set; }
    /// <summary>
    /// If and how much reward was added because item is one of daily items
    /// </summary>
    public int DailyItemReward { get; set; }
    /// <summary>
    /// How much was added because of daily quest
    /// </summary>
    public int DailyQuestReward { get; set; }
}

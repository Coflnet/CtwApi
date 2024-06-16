public class ImageUploadEvent
{
    public string ImageUrl { get; set; }
    public string label { get; set; }
    public int Exp { get; set; }
    public Guid UserId { get; set; }
    public bool IsUnique { get; set; }
    public bool IsCurrent { get; set; }
    public long ImageReward { get; set; }
}
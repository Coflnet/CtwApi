public class UploadImageResponse
{
    public CapturedImage Image { get; set; }
    public UploadRewards Rewards { get; set; }
    public UploadStats Stats { get; set; } = new();
}

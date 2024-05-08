public class CapturedImage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ObjectLabel { get; set; }
    public string Description { get; set; }
    public int Day { get; set; }
    public int Verifications { get; set; }
    /// <summary>
    /// Can contain "segmentation", "ocr" etc
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
}
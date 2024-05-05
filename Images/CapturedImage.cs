public class CapturedImage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ObjectId { get; set; }
    public int Verifications { get; set; }
    public string Metadata { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
}
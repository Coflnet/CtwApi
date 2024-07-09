namespace Coflnet.Auth;

public class ConsentData
{
    /// <summary>
    /// When the consent was given, updated when storing
    /// </summary>
    public DateTimeOffset? GivenAt { get; set; }
    /// <summary>
    /// Accepted all targeting
    /// </summary>
    public bool? TargetedAds { get; set; }
    /// <summary>
    /// Accepted (ad) tracking
    /// </summary>
    public bool? Tracking { get; set; }
    /// <summary>
    /// Analytics
    /// </summary>
    public bool? Analytics { get; set; }
    /// <summary>
    /// Licenses for content
    /// </summary>
    public bool? AllowResell { get; set; }
    /// <summary>
    /// Allow to use data for new services, AI, etc.
    /// </summary>
    public bool? NewService { get; set; }
}
namespace Coflnet.Auth;

public class User
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Locale { get; set; }
    /// <summary>
    /// Based on the auth provider, this is the ID of the user in the auth provider's system.
    /// </summary>
    public string AuthProviderId { get; set; }
    /// <summary>
    /// The auth provider used to authenticate the user.
    /// </summary>
    public string AuthProvider { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }


    public enum AuthProviderType
    {
        /// <summary>
        /// Hash of a secret token.
        /// </summary>
        SecretHash,
        Google,
        Facebook,
        Twitter,
        GitHub,
    }
}
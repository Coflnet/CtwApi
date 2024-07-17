using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Cassandra;
using ISession = Cassandra.ISession;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Cryptography;
using Coflnet.Core;

namespace Coflnet.Auth;

public class AuthService
{
    private readonly Table<User> userDb;
    private readonly IConfiguration config;
    private readonly ILogger<AuthService> logger;

    public AuthService(ISession session, IConfiguration config, ILogger<AuthService> logger)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<User>()
            .PartitionKey(t => t.AuthProviderId)
            .Column(t => t.Id, cm => cm.WithSecondaryIndex())
            .Column(t => t.Email, cm => cm.WithSecondaryIndex())
            .Column(t => t.AuthProvider, cm => cm.WithSecondaryIndex())
        );
        userDb = new Table<User>(session, mapping, "users");
        userDb.CreateIfNotExists();
        this.config = config;
        this.logger = logger;
        var adminToken = CreateTokenFor("admin");
        logger.LogInformation($"Admin token: {adminToken}");
    }

    public Guid GetUserId(string authProviderId)
    {
        return userDb.Where(u => u.AuthProviderId == authProviderId).Select(u => u.Id).Execute().FirstOrDefault();
    }

    public Guid CreateUser(string authProviderId, string name, string? email, string? locale, Guid? userId = null)
    {
        var user = new User()
        {
            Id = userId ?? Guid.NewGuid(),
            Name = name,
            Email = email,
            AuthProviderId = authProviderId,
            Locale = locale,
            CreatedAt = DateTime.UtcNow,
        };
        user.LastSeenAt = user.CreatedAt;
        userDb.Insert(user).Execute();
        return user.Id;
    }

    public User? GetUser(Guid userId)
    {
        return userDb.Where(u => u.Id == userId).Execute().FirstOrDefault();
    }

    public User? GetUser(string authProviderId)
    {
        return userDb.Where(u => u.AuthProviderId == authProviderId).Execute().FirstOrDefault();
    }

    public string GetTokenAnonymous(string secret, string? ip, string? userAgentOrDeviceInfo, string locale, Guid? existing = null)
    {
        if (secret.Length < 32)
        {
            throw new ApiException("validation", "Secret must be at least 32 characters long");
        }
        var virtualid = Encoding.UTF8.GetString(SHA512.HashData(Encoding.UTF8.GetBytes(secret)));
        var user = GetUser(virtualid);
        if (user == null)
        {
            // TODO: rate limit
            var userId = CreateUser(virtualid, "Anonymous", null, locale, existing);
            return CreateTokenFor(userId);
        }
        // update last seen at and return token
        user.LastSeenAt = DateTime.UtcNow;
        userDb.Insert(user).Execute();
        return CreateTokenFor(user.Id);
    }


    public string CreateTokenFor(Guid userId)
    {
        return CreateTokenFor(userId.ToString());
    }
    private string CreateTokenFor(string userId)
    {
        string key = config["jwt:secret"]; //Secret key which will be used later during validation
        var issuer = config["jwt:issuer"];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        //Create a List of Claims, Keep claims name short    
        var permClaims = new List<Claim>();
        permClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
        // userLevel
        permClaims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()));

        //Create Security Token object by giving required parameters    
        var token = new JwtSecurityToken(issuer, //Issure    
            issuer, //Audience    
            permClaims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: credentials);
        var jwt_token = new JwtSecurityTokenHandler().WriteToken(token);
        return jwt_token;
    }

    internal async Task DeleteUser(Guid userId)
    {
        var userEntries = userDb.Where(u => u.Id == userId).Execute();
        foreach (var user in userEntries)
        {
            userDb.Where(u => u.AuthProviderId == user.AuthProviderId).Delete().Execute();
        }
    }
}

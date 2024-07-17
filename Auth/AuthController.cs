using Cassandra.Data.Linq;
using Coflnet.Auth;
using Coflnet.Core;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RestSharp;

namespace Coflnet.Auth;

[Route("api")]
[ApiController]
public class AuthController : Controller
{
    private readonly AuthService authService;
    private readonly ILogger<AuthController> logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        this.authService = authService;
        this.logger = logger;
    }

    [HttpPost("auth/anonymous")]
    [ProducesResponseType(200)]
    public TokenResponse Login([FromBody] AnonymousLoginRequest request)
    {
        var ipBehindCloudflare = Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        var token = authService.GetTokenAnonymous(request.Secret, ipBehindCloudflare, Request.Headers["User-Agent"], request.Locale ?? "en");
        return new TokenResponse { Token = token };
    }
    /// <summary>
    /// When you authorized based on an auth provider add the current device id to authorized secets
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("auth/deviceId")]
    [ProducesResponseType(200)]
    [Authorize]
    public TokenResponse LoginDevice([FromBody] AnonymousLoginRequest request)
    {
        var userId = this.GetUserId();
        var token = authService.GetTokenAnonymous(request.Secret, null, Request.Headers["User-Agent"], request.Locale ?? "en", userId);
        return new TokenResponse { Token = token };
    }

    /// <summary>
    /// Authenticates with google and returns a jwt token
    /// </summary>
    /// <param name="authCode"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("auth/google")]
    [Consumes("application/json")]
    public async Task<TokenResponse> AuthWithGoogle([FromBody] AuthToken authCode)
    {
        try
        {
            GoogleJsonWebSignature.Payload? data = await GetSignedPayload(authCode);
            return await GetTokenForUser(data);
        }
        catch (TokenResponseException e)
        {
            logger.LogError(e, "Failed to get google token");
            throw new ApiException("invalid_token", $"The token is invalid, {e.Error}");
        }
    }

    private async Task<GoogleJsonWebSignature.Payload?> GetSignedPayload(AuthToken authCode)
    {
        GoogleJsonWebSignature.Payload? data = null;
        if (authCode.Token.StartsWith("ey"))
        {
            // already a jwt token
            data = await ValidateToken(authCode.Token);
            logger.LogInformation($"Got google user id token: {data.Subject} {data.Name}");
        }
        else
        {
            data = await GetFromAccessToken(authCode);
            logger.LogInformation($"Got google user: {data.Subject} {data.Name}");
        }

        return data;
    }

    [HttpPost]
    [Route("auth/google/connect")]
    [Authorize]
    [Consumes("application/json")]
    public async Task<TokenResponse> ConnectGoogle([FromBody] AuthToken authCode)
    {
        var data = await GetSignedPayload(authCode);
        var currentUserId = this.GetUserId();
        var userId = authService.GetUserId(data.Subject);
        if (userId == currentUserId)
        {
            return new TokenResponse() { Token = authService.CreateTokenFor(userId) };
        }
        if (userId != default)
        {
            throw new ApiException("already_connected", "This google account is already connected to another user");
        }
        // create connection
        authService.CreateUser(data.Subject, data.Name, data.Email, data.Locale, currentUserId);
        return new TokenResponse() { Token = authService.CreateTokenFor(userId) };
    }

    private async Task<GoogleJsonWebSignature.Payload> ValidateToken(string token)
    {
        try
        {
            var tokenData = await GoogleJsonWebSignature.ValidateAsync(token);
            Console.WriteLine("google user: " + tokenData.Name);
            return tokenData;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to validate google token");
            throw new ApiException("internal_error", $"{e.InnerException?.Message}");
        }
    }

    private async Task<TokenResponse> GetTokenForUser(GoogleJsonWebSignature.Payload data)
    {
        var userId = authService.GetUserId(data.Subject);

        if (userId == default)
        {
            // "register"
            userId = authService.CreateUser(data.Subject, data.Name, data.Email, data.Locale);
        }
        return new TokenResponse() { Token = authService.CreateTokenFor(userId) };
    }

    private static async Task<GoogleJsonWebSignature.Payload?> GetFromAccessToken(AuthToken authCode)
    {

        // get user info with accesstoken
        RestClient client = new RestClient($"https://www.googleapis.com/oauth2/v3/userinfo?access_token=" + authCode.Token);
        RestRequest request = new RestRequest();
        var response = await client.ExecuteAsync(request);
        var data = JsonConvert.DeserializeObject<GoogleJsonWebSignature.Payload>(response.Content);
        return data;
    }
}

public class AuthToken
{
    public string Token { get; set; }
}

public class TokenResponse
{
    public string Token { get; set; }

}

public class AnonymousLoginRequest
{
    public string Secret { get; set; }
    public string Locale { get; set; }
}

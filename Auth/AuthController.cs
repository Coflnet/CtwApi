using Coflnet.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Auth;

[Route("api")]
[ApiController]
public class AuthController : Controller
{
    private readonly AuthService authService;

    public AuthController(AuthService authService)
    {
        this.authService = authService;
    }

    [HttpPost("auth/anonymous")]
    [ProducesResponseType(200)]
    public TokenResponse Login([FromBody] AnonymousLoginRequest request)
    {
        var ipBehindCloudflare = Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        var token = authService.GetTokenAnonymous(request.Secret, ipBehindCloudflare, Request.Headers["User-Agent"], request.Locale ?? "en");
        return new TokenResponse { Token = token };
    }
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

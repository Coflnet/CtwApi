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
    public IActionResult Login([FromBody] AnonymousLoginRequest request)
    {
        var ipBehindCloudflare = Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        var token = authService.GetTokenAnonymous(request.Secret, ipBehindCloudflare, Request.Headers["User-Agent"]);
        return Ok(new { token });
    }
}

public class AnonymousLoginRequest
{
    public string Secret { get; set; }
}

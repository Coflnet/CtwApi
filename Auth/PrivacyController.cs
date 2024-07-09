using Coflnet.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Auth;

[ApiController]
[Route("api/privacy")]
public class PrivacyController : ControllerBase
{
    private readonly PrivacyService privacyService;

    public PrivacyController(PrivacyService privacyService)
    {
        this.privacyService = privacyService;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ConsentData>> GetConsent()
    {

        var consent = await privacyService.GetConsent(this.GetUserId());
        if (consent == null)
        {
            return NotFound();
        }
        return consent;
    }

    [HttpPost]
    [Authorize]
    public ConsentData SaveConsent(ConsentData consent)
    {
        consent.UserId = this.GetUserId();
        privacyService.SaveConsent(consent);
        return consent;
    }
}

public static class ControllerExtension
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        return Guid.Parse(controller.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
    }
}
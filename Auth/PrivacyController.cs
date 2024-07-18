using Coflnet.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Auth;

[ApiController]
[Route("api/privacy")]
public class PrivacyController : ControllerBase
{
    private readonly PrivacyService privacyService;
    private readonly DeletionService deletionService;

    public PrivacyController(PrivacyService privacyService, DeletionService deletionService)
    {
        this.privacyService = privacyService;
        this.deletionService = deletionService;
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
        privacyService.SaveConsent(this.GetUserId(), consent);
        return consent;
    }

    [HttpDelete("/api/account")]
    [Authorize]
    public async Task<DateTimeOffset> DeleteAccount()
    {
        return await deletionService.RequestDeletion(this.GetUserId());
    }
    [HttpGet("/api/account/deletion")]
    [Authorize]
    public async Task<DateTimeOffset?> GetDeletiontime()
    {
        return await deletionService.DeletingAt(this.GetUserId());
    }
    [HttpPost("/api/account/deletion/abort")]
    [Authorize]
    public async Task AbortDeletion()
    {
        await deletionService.AbortDeletion(this.GetUserId());
    }
}

public static class ControllerExtension
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        return Guid.Parse(controller.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
    }
}
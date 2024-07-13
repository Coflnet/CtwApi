using Coflnet.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly EventStorageService expService;

    public EventsController(EventStorageService expService)
    {
        this.expService = expService;
    }

    [HttpGet]
    [Route("history")]
    [Authorize]
    public async Task<IEnumerable<ChangeEvent>> GetExpChanges(DateTime since = default)
    {
        return await expService.GetChanges(this.GetUserId(), since);
    }
}
using Coflnet.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("exp")]
public class ExpController : ControllerBase
{
    private readonly ExpService expService;

    public ExpController(ExpService expService)
    {
        this.expService = expService;
    }

    [HttpGet]
    [Route("history")]
    public async Task<IEnumerable<ExpChange>> GetExpChanges()
    {
        return await expService.GetExpChanges(this.GetUserId());
    }
}
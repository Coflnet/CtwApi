using Coflnet.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class StatsController : ControllerBase
{
    private readonly StatsService statsService;
    private readonly ILogger<StatsController> logger;

    public StatsController(StatsService statsService, ILogger<StatsController> logger)
    {
        this.statsService = statsService;
        this.logger = logger;
    }

    [HttpGet("stats")]
    [Authorize]
    public async Task<IEnumerable<Stat>> GetStats()
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
        return await statsService.GetStats(userId);
    }

    [HttpGet("stats/{statName}")]
    [Authorize]
    public async Task<long> GetStat(string statName)
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
        return await statsService.GetStat(userId, statName);
    }
}

using Cassandra.Data.Linq;
using Coflnet.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/leaderboard")]
[Authorize]
public class LeaderboardController : ControllerBase
{
    private readonly LeaderboardService leaderboardService;

    public LeaderboardController(LeaderboardService leaderboardService)
    {
        this.leaderboardService = leaderboardService;
    }

    [HttpGet("boards")]
    public BoardNames GetAvailableLeaderboards()
    {
        return new BoardNames();
    }

    [HttpGet("{leaderboardId}")]
    public async Task<IEnumerable<LeaderboardService.BoardEntry>> GetLeaderboard(string leaderboardId, int offset = 0, int count = 10)
    {
        return await leaderboardService.GetLeaderboard(leaderboardId, offset, count);
    }

    [HttpGet("{leaderboardId}/me")]
    public async Task<IEnumerable<LeaderboardService.BoardEntry>> GetLeaderboardAroundMe(string leaderboardId, int count = 10)
    {
        return await leaderboardService.GetLeaderboardAroundMe(leaderboardId, GetUserId(), count);
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
    }

    [HttpGet("profile")]
    public async Task<LeaderboardService.PublicProfile> GetProfile()
    {
        return await leaderboardService.GetProfile(GetUserId());
    }

    [HttpPost("profile")]
    public async Task SetProfile([FromBody] LeaderboardService.PublicProfile profile)
    {
        var userId = GetUserId();
        await leaderboardService.SetProfile(userId, profile.Name, profile.Avatar);
    }

    [HttpGet("{leaderboardId}/me/rank")]
    public async Task<long> GetRank(string leaderboardId)
    {
        return await leaderboardService.GetRankOf(leaderboardId, GetUserId());
    }

    [HttpGet("me/ranks")]
    public async Task<RankSummary> GetRanks()
    {
        return await leaderboardService.GetRanks(GetUserId());
    }
}

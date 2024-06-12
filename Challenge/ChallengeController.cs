using Coflnet.Auth;
using Microsoft.AspNetCore.Mvc;
/// <summary>
/// Retrieve info about current challanges and their status
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChallengeController : ControllerBase
{
    private readonly ChallengeService challengeService;

    public ChallengeController(ChallengeService challengeService)
    {
        this.challengeService = challengeService;
    }
    /// <summary>
    /// Get the daily challenge for the user
    /// </summary>
    /// <returns></returns>
    [HttpGet("daily")]
    public async Task<ChallengeResponse> Challenge()
    {
        return await challengeService.GetDailyChallenges(User.UserId());
    }

    public class ChallengeResponse
    {
        public bool Success { get; set; }
        public Challenge[] Challenges { get; set; }
    }
}

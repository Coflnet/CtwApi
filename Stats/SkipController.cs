using Coflnet.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SkipController : ControllerBase
{
    private readonly SkipService skipService;

    public SkipController(SkipService skipService)
    {
        this.skipService = skipService;
    }

    [HttpPost("skip/{label}")]
    public async Task<SkipResponse> Skip(string label)
    {
        if (await skipService.TrySkip(User.UserId(), label))
        {
            return new SkipResponse() { Success = true };
        }
        return new SkipResponse() { Success = false };
    }

    [HttpGet("available")]
    public async Task<SkipsAvailable> Available()
    {
        var (used, collected) = await skipService.SkipStat(User.UserId());
        return new SkipsAvailable() { Used = used, Total = 2 - used + collected };
    }

    public class SkipResponse
    {
        public bool Success { get; set; }
    }
    public class SkipsAvailable
    {
        public int Total { get; set; }
        public int Used { get; set; }
    }
}
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class MultiplierController : ControllerBase
{
    private readonly MultiplierService multiplierService;

    public MultiplierController(MultiplierService multiplierService)
    {
        this.multiplierService = multiplierService;
    }

    [HttpGet("multiplier")]
    public async Task<MultiplierResponse> Multiplier()
    {
        var active = await multiplierService.GetMultipliers();
        return new MultiplierResponse() { Success = true, Multiplier = active };
    }

    public class MultiplierResponse
    {
        public bool Success { get; set; }
        public ActiveMultiplier[] Multiplier { get; set; }
    }
}

public class ActiveMultiplier
{
    public float Multiplier { get; set; }
    public string Category { get; set; }
}

public class MultiplierService
{
    private readonly ObjectService objectService;
    private readonly RewardsConfig rewardsConfig;

    public MultiplierService(ObjectService objectService, RewardsConfig rewardsConfig)
    {
        this.objectService = objectService;
        this.rewardsConfig = rewardsConfig;
    }

    public async Task<ActiveMultiplier[]> GetMultipliers()
    {
        var categories = await objectService.GetCategories();
        var today = DateTime.Now.Date;
        var random = new Random(today.DayOfYear * today.Year);
        var selected = categories.OrderBy(c => random.Next()).Take(3).Select(c => new ActiveMultiplier() { Category = c.Name }).ToArray();
        selected[0].Multiplier = rewardsConfig.MultiplierRewards.Third;
        selected[1].Multiplier = rewardsConfig.MultiplierRewards.Second;
        selected[2].Multiplier = rewardsConfig.MultiplierRewards.Top;
        return selected;
    }
}
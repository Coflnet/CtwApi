using Coflnet.Auth;
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

    public MultiplierService(ObjectService objectService)
    {
        this.objectService = objectService;
    }

    public async Task<ActiveMultiplier[]> GetMultipliers()
    {
        var categories = await objectService.GetCategories();
        var today = DateTime.Now.Date;
        var random = new Random(today.DayOfYear * today.Year);
        var selected = categories.OrderBy(c => random.Next()).Take(3).Select(c => new ActiveMultiplier() { Category = c.Name }).ToArray();
        selected[0].Multiplier = 1.25f;
        selected[1].Multiplier = 2f;
        selected[2].Multiplier = 4f;
        return selected;
    }
}
using Coflnet.Core;
using Confluent.Kafka;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public class ObjectController : ControllerBase
{
    private readonly ObjectService objectService;
    private readonly ILogger<ObjectController> logger;

    public ObjectController(ObjectService objectService, ILogger<ObjectController> logger)
    {
        this.objectService = objectService;
        this.logger = logger;
    }

    [HttpGet("objects")]
    [Authorize]
    public async Task<IEnumerable<CollectableObject>> GetObjects()
    {
        return await objectService.GetObjects("en");
    }

    /// <summary>
    /// Returns the newest and highest rewarded objects to collect
    /// </summary>
    /// <returns></returns>
    [HttpGet("new")]
    [Authorize]
    public async Task<IEnumerable<CollectableObject>> GetNewObjects(int offset = 0, int count = 10)
    {
        return (await objectService.GetObjects("en")).OrderByDescending(o => o.Value).Skip(offset).Take(count);
    }

    [HttpGet("objects/next")]
    [Authorize]
    public async Task<CollectableObject?> GetNextObject()
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
        return await objectService.GetNextObjectToCollect(userId);
    }

    [HttpGet("objects/daily")]
    [Authorize]
    public async Task<List<CollectableObject>> GetDailyObject()
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
        return await objectService.GetDailyObjects(userId);
    }

    [HttpGet("objects/challenge")]
    [Authorize]
    public async Task<List<CollectableObject>> GetChellenge(int offset = -1, int count = 5)
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
        return await objectService.GetRandom(userId, offset, count);
    }


    [HttpGet("objects/categories")]
    [Authorize]
    public async Task<IEnumerable<Category>> GetCategories()
    {
        return await objectService.GetCategories();
    }

    [HttpGet("objects/{objectId}")]
    [Authorize]
    public async Task<CollectableObject?> GetObject(string objectId)
    {
        return await objectService.GetObject("en", objectId);
    }

    [HttpGet("objects/category/{categoryName}")]
    [Authorize]
    public async Task<IEnumerable<CollectableObject>> GetCategoryObjects(string categoryName)
    {
        return await objectService.GetCategoryObjects(categoryName);
    }
}

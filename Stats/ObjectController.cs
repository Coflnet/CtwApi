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
        return await objectService.GetObject(objectId);
    }

    [HttpGet("objects/category/{categoryName}")]
    [Authorize]
    public async Task<IEnumerable<CollectableObject>> GetCategoryObjects(string categoryName)
    {
        return await objectService.GetCategoryObjects(categoryName);
    }
}

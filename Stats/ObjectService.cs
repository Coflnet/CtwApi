using System.Text.Json;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

public class ObjectService
{
    private readonly Table<CollectableObject> objectTable;
    private ILogger<ObjectService> logger;

    public ObjectService(ISession session, ILogger<ObjectService> logger)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<CollectableObject>()
            .PartitionKey(t => t.Category)
            .ClusteringKey(t => t.Name)
            .Column(t => t.UserId, cm => cm.WithSecondaryIndex())
            .Column(t => t.ObjectId, cm => cm.WithSecondaryIndex())
        );
        objectTable = new Table<CollectableObject>(session, mapping, "objects");
        objectTable.CreateIfNotExists();
        var existing = objectTable.FirstOrDefault().Execute();
        if (existing == null)
        {
            Task.Run(CreateObjects);
        }

        this.logger = logger;
    }

    private async Task CreateObjects()
    {
        try
        {
            var things = await GetThings();
            foreach (var category in things.Keys)
            {
                var existingInCategory = await objectTable.Where(o => o.Category == category).ExecuteAsync();
                foreach (var name in things[category])
                {
                    if (existingInCategory.Any(o => o.Name == name))
                    {
                        continue;
                    }
                    await CreateObject(Guid.NewGuid(), category, name, "", 500);
                }
            }
        }
        catch (System.Exception e)
        {
            logger.LogError(e, "Failed to create objects");
        }
    }

    public async Task<CollectableObject> CreateObject(Guid userId, string category, string name, string description, long value)
    {
        var newObject = new CollectableObject()
        {
            ObjectId = Guid.NewGuid(),
            UserId = userId,
            Category = category,
            Name = name,
            Description = description,
            Value = value
        };
        await objectTable.Insert(newObject).ExecuteAsync();
        logger.LogInformation($"Created object {newObject.ObjectId} in category {category} with name {name} and value {value}");
        return newObject;
    }

    public async Task<IEnumerable<CollectableObject>> GetObjects(Guid userId)
    {
        return await objectTable.Where(o => o.UserId == userId).ExecuteAsync();
    }

    public async Task<CollectableObject?> GetObject(Guid userId, Guid objectId)
    {
        return (await objectTable.Where(o => o.UserId == userId && o.ObjectId == objectId).ExecuteAsync()).FirstOrDefault();
    }

    public async Task<IEnumerable<CollectableObject>> GetCategoryObjects(string category)
    {
        return await objectTable.Where(o => o.Category == category).ExecuteAsync();
    }

    public async Task DecreaseValueTo(Guid objectId, long value)
    {
        await objectTable.Where(o => o.ObjectId == objectId)
            .Select(o => new CollectableObject() { Value = value })
            .Update().ExecuteAsync();
    }

    public async Task<CollectableObject?> GetObject(Guid objectId)
    {
        return (await objectTable.Where(o => o.ObjectId == objectId).ExecuteAsync()).FirstOrDefault();
    }

    public async Task<IEnumerable<Category>> GetCategories()
    {
        Dictionary<string, string[]>? categories = await GetThings();
        return categories.Keys.Select(k => new Category() { Name = k });
    }

    private static async Task<Dictionary<string, string[]>?> GetThings()
    {
        var text = await File.ReadAllTextAsync("words.json");
        var categories = JsonSerializer.Deserialize<Dictionary<string, string[]>>(text);
        return categories;
    }
}

public class Category
{
    public string Name { get; set; }
}

public class CollectableObject
{
    public Guid ObjectId { get; set; }
    public Guid UserId { get; set; }
    public string Category { get; set; }
    public string Name { get; set; }
    public Dictionary<string, string> Localizations { get; set; }
    public string Description { get; set; }
    public long Value { get; set; }
}
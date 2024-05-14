using System.Text.Json;
using System.Text.Json.Serialization;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

public class ObjectService
{
    private readonly Table<CollectableObject> objectTable;
    private ILogger<ObjectService> logger;
    private StatsService statsService;

    public ObjectService(ISession session, ILogger<ObjectService> logger, StatsService statsService)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<CollectableObject>()
            .PartitionKey(t => t.Locale)
            .ClusteringKey(t => t.Name)
            .Column(t => t.UserId, cm => cm.WithSecondaryIndex())
            .Column(t => t.Category, cm => cm.WithSecondaryIndex())
        );
        objectTable = new Table<CollectableObject>(session, mapping, "name_objects");
        objectTable.CreateIfNotExists();
        var existing = objectTable.FirstOrDefault().Execute();
        if (existing == null)
        {
            session.Execute("DROP TABLE IF EXISTS objects");
            Task.Run(CreateObjects);
        }

        this.logger = logger;
        this.statsService = statsService;
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
                    await CreateObject(Guid.Empty, "en", category, name, "", 500);
                }
            }
        }
        catch (System.Exception e)
        {
            logger.LogError(e, "Failed to create objects");
        }
    }

    public async Task<CollectableObject> CreateObject(Guid userId, string locale, string category, string name, string description, long value)
    {
        var newObject = new CollectableObject()
        {
            UserId = userId,
            Locale = locale,
            Category = category,
            Name = name,
            Description = description,
            Value = value
        };
        await objectTable.Insert(newObject).ExecuteAsync();
        logger.LogInformation($"Created object in category {category} with name {name} and value {value}");
        return newObject;
    }

    public async Task<IEnumerable<CollectableObject>> GetObjects(Guid userId)
    {
        return await objectTable.Where(o => o.UserId == userId).ExecuteAsync();
    }

    public async Task<IEnumerable<CollectableObject>> GetObjects(string locale)
    {
        return await objectTable.Where(o => o.Locale == locale).ExecuteAsync();
    }

    public async Task<IEnumerable<CollectableObject>> GetCategoryObjects(string category)
    {
        return await objectTable.Where(o => o.Category == category).ExecuteAsync();
    }

    public async Task DecreaseValueTo(string objectName, long value)
    {
        await objectTable.Where(o => o.Name == objectName)
            .Select(o => new CollectableObject() { Value = value })
            .Update().ExecuteAsync();
    }

    public async Task<CollectableObject?> GetObject(string name)
    {
        return (await objectTable.Where(o => o.Name == name).ExecuteAsync()).FirstOrDefault();
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

    public async Task<CollectableObject?> GetNextObjectToCollect(Guid userId)
    {
        var stat = (int) await statsService.GetStat(userId, "objects_collected");
        var things = await GetThings();
        var random = new Random(userId.GetHashCode());
        var target = things?.SelectMany(t => t.Value).OrderBy(t => random.Next()).Skip(stat).FirstOrDefault();

        var objects = await objectTable.Where(o => o.Locale == "en" && o.Name == target).ExecuteAsync();
        return objects.FirstOrDefault();
    
    }
}

public class Category
{
    public string Name { get; set; }
}

public class CollectableObject
{
    [JsonIgnore]
    public Guid UserId { get; set; }
    // add added date
    public string Category { get; set; }
    public string Name { get; set; }
    public string Locale { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public string Description { get; set; }
    public long Value { get; set; }
}
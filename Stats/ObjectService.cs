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
        var existing = objectTable.Where(o => o.Locale == "en").FirstOrDefault().Execute();
        if (existing == null)
        {
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

    public async Task DecreaseValueTo(string locale, string objectName, long value)
    {
        await objectTable.Where(o => o.Name == objectName && o.Locale == locale)
            .Select(o => new CollectableObject() { Value = value })
            .Update().ExecuteAsync();
    }

    public async Task<CollectableObject?> GetObject(string locale, string name)
    {
        return (await objectTable.Where(o => o.Name == name && o.Locale == locale).ExecuteAsync()).FirstOrDefault();
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

    public async Task<string?> CurrentLabeltoCollect(Guid userId)
    {
        var stat = (int)await statsService.GetStat(userId, "current_offset");
        var things = await GetThings();
        Random random = UserRandom(userId);
        return things?.SelectMany(t => t.Value).OrderBy(t => random.Next()).Skip(stat).FirstOrDefault();
    }

    private static Random UserRandom(Guid userId)
    {
        var intFromFirstBytesofUserId = Convert.ToInt32(userId.ToString("N").Substring(0, 8), 16);
        var random = new Random(intFromFirstBytesofUserId);
        return random;
    }

    public async Task<CollectableObject?> GetNextObjectToCollect(Guid userId)
    {
        var target = await CurrentLabeltoCollect(userId);
        var objects = await objectTable.Where(o => o.Locale == "en" && o.Name == target).ExecuteAsync();
        return objects.FirstOrDefault();
    }

    public async Task<Dictionary<string, int>> GetDailyLabels(Guid userId)
    {
        var things = await GetThings();
        var random = UserRandom(userId);
        var thingsList = things?.SelectMany(t => t.Value).ToList() ?? new();
        var target = thingsList.OrderBy(t => random.Next()).Skip(DateTime.UtcNow.DayOfYear * 15 % thingsList.Count).Take(15).ToList() ?? new();

        return target.Select(t => (t, 250 - (int)Math.Sqrt(random.Next(1, 65)) * 25)).ToDictionary(target => target.t, target => target.Item2);
    }

    public async Task<List<CollectableObject>> GetDailyObjects(Guid userId)
    {
        var things = await GetDailyLabels(userId);
        Console.WriteLine($"Daily objects: {string.Join(", ", things.Select(t => $"{t.Key} ({t.Value})"))}");
        var labels = things.Keys;
        var objects = (await objectTable.Where(o => o.Locale == "en" && labels.Contains(o.Name)).ExecuteAsync()).ToList();
        foreach (var obj in objects)
        {
            obj.Value = things[obj.Name];
        }
        return objects.ToList();
    }

    internal async Task<List<CollectableObject>> GetRandom(Guid userId, int offset, int count)
    {
        var things = await GetThings();
        var random = new Random(userId.GetHashCode());
        if (offset == -1)
        {
            offset = random.Next(0, (things?.SelectMany(t => t.Value).Count() ?? 2) - 2);
        }
        var target = things?.SelectMany(t => t.Value).OrderBy(t => random.Next()).Skip(offset).Take(count).ToList() ?? new();

        var objects = await objectTable.Where(o => o.Locale == "en" && target.Contains(o.Name)).ExecuteAsync();
        return objects.ToList();
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
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

public class ObjectService
{
    private readonly Table<CollectableObject> objectTable;

    public ObjectService(ISession session)
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
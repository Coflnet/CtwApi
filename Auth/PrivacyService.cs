using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Auth;

public class PrivacyService
{
    private readonly Table<ConsentData> consentDb;

    public PrivacyService(ISession session)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<ConsentData>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.GivenAt, SortOrder.Descending)
            .Column(t => t.TargetedAds)
            .Column(t => t.Tracking)
            .Column(t => t.Analytics)
            .Column(t => t.AllowResell)
            .Column(t => t.NewService)
        );
        consentDb = new Table<ConsentData>(session, mapping, "consent");
        consentDb.CreateIfNotExists();
    }

    public void SaveConsent(ConsentData consent)
    {
        consentDb.Insert(consent).Execute();
    }

    public async Task<ConsentData> GetConsent(Guid userId)
    {
        return await consentDb.Where(c => c.UserId == userId).FirstOrDefault().ExecuteAsync();
    }
}

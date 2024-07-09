using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;
using AutoMapper;

namespace Coflnet.Auth;

public class PrivacyService
{
    private readonly Table<InternalConsentData> consentDb;
    private readonly AutoMapper.IMapper mapper;

    public PrivacyService(ISession session, AutoMapper.IMapper mapper)
    {
        var mapping = new MappingConfiguration()
            .Define(new Map<InternalConsentData>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.GivenAt, SortOrder.Descending)
            .Column(t => t.TargetedAds)
            .Column(t => t.Tracking)
            .Column(t => t.Analytics)
            .Column(t => t.AllowResell)
            .Column(t => t.NewService)
        );
        consentDb = new Table<InternalConsentData>(session, mapping, "consent");
        consentDb.CreateIfNotExists();
        this.mapper = mapper;
    }

    public void SaveConsent(Guid guid, ConsentData consent)
    {
        var internalConsent = mapper.Map<InternalConsentData>(consent);
        internalConsent.UserId = guid;
        internalConsent.GivenAt = DateTimeOffset.UtcNow;
        consentDb.Insert(internalConsent).Execute();
    }

    public async Task<ConsentData> GetConsent(Guid userId)
    {
        var internalConsent = await consentDb.Where(c => c.UserId == userId).FirstOrDefault().ExecuteAsync();
        return mapper.Map<ConsentData>(internalConsent);
    }

    public class InternalConsentData
    {
        public Guid UserId { get; set; }
        public DateTimeOffset GivenAt { get; set; }
        public bool TargetedAds { get; set; }
        public bool Tracking { get; set; }
        public bool Analytics { get; set; }
        public bool AllowResell { get; set; }
        public bool NewService { get; set; }
    }
}

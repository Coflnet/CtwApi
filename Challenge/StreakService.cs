using Cassandra.Data.Linq;
using Cassandra.Mapping;

public class StreakService
{
    private readonly StatsService statsService;
    private readonly ILogger<StreakService> logger;
    private Table<Streak> streakTable;
    private readonly EventBusService eventBus;

    public StreakService(StatsService statsService, Cassandra.ISession session, ILogger<StreakService> logger, EventBusService eventBus)
    {
        this.statsService = statsService;
        this.logger = logger;
        this.eventBus = eventBus;
        eventBus.ImageUploaded += (sender, e) =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await HandleImageUpload(e);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to handle image upload");
                }
            });
        };

        var mapping = new MappingConfiguration()
            .Define(new Map<Streak>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.Date, SortOrder.Descending)
        );
        streakTable = new Table<Streak>(session, mapping, "streaks");
        streakTable.CreateIfNotExists();
    }

    private async Task HandleImageUpload(ImageUploadEvent e)
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var streak = streakTable.Where(s => s.UserId == e.UserId && s.Date >= yesterday).FirstOrDefault().Execute();
        if (streak == null)
        {
            streak = new Streak()
            {
                UserId = e.UserId,
                Date = DateTime.Today,
                StreakLength = 0,
                LabelExtendingStreak = e.label,
                ImageUrl = e.ImageUrl
            };
            var streakStat = await statsService.GetStat(e.UserId, "collection_streak");
            if (streakStat > 0)
                await statsService.IncreaseStat(e.UserId, "collection_streak", streakStat);
        }
        else if (streak.Date == DateTime.Today)
        {
            return; // already extended
        }
        else
        {
            streak.StreakLength++;
            streak.LabelExtendingStreak = e.label;
            streak.ImageUrl = e.ImageUrl;
            streak.Date = DateTime.Today;
            await statsService.IncreaseStat(e.UserId, "collection_streak", 1);
        }
        var insert = streakTable.Insert(streak);
        insert.SetTTL(86400 * 5);
        await insert.ExecuteAsync();
    }

    public class Streak
    {
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public int StreakLength { get; set; }
        public string LabelExtendingStreak { get; set; }
        public string ImageUrl { get; set; }
    }
}

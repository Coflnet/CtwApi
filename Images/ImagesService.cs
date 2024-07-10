using Amazon.S3;
using Amazon.S3.Model;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Auth;
using Coflnet.Core;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using ISession = Cassandra.ISession;


public class ImagesService
{
    private readonly ILogger<ImagesService> logger;
    private readonly IAmazonS3 s3Client;
    private readonly string bucketName;
    private readonly Table<CapturedImage> imageTable;
    private readonly Table<ImageStatCounter> imageStatCounterTable;
    private readonly StatsService statsService;
    private readonly ObjectService objectService;
    private readonly LeaderboardService leaderboardService;
    private readonly SkipService skipService;
    private readonly EventBusService eventBus;
    private readonly MultiplierService multiplierService;
    private readonly WordService wordService;
    private readonly StreakService streakService;
    private readonly PrivacyService privacyService;
    private readonly ExpService expService;

    public ImagesService(ILogger<ImagesService> logger,
                         IAmazonS3 s3Client,
                         IConfiguration config,
                         ISession session,
                         StatsService statsService,
                         ObjectService objectService,
                         LeaderboardService leaderboardService,
                         EventBusService eventBus,
                         SkipService skipService,
                         MultiplierService multiplierService,
                         WordService wordService,
                         StreakService streakService,
                         PrivacyService privacyService,
                         ExpService expService)
    {
        this.logger = logger;
        this.s3Client = s3Client;
        this.bucketName = config["BUCKET_NAME"] ?? throw new ArgumentNullException("BUCKET_NAME");
        var mapping = new MappingConfiguration()
            .Define(new Map<CapturedImage>()
            .PartitionKey(t => t.ObjectLabel)
            .ClusteringKey(t => t.UserId)
            .ClusteringKey(t => t.Day)
            .Column(t => t.Id, cm => cm.WithSecondaryIndex())
            .Column(t => t.UserId, cm => cm.WithSecondaryIndex())
            .Column(t => t.ContentType)
            .Column(t => t.Size)
        );
        imageTable = new Table<CapturedImage>(session, mapping, "named_images");
        imageTable.CreateIfNotExists();
        var counterMapping = new MappingConfiguration()
            .Define(new Map<ImageStatCounter>()
            .PartitionKey(t => t.Label)
            .Column(t => t.CollectCount, cm => cm.AsCounter())
        );
        imageStatCounterTable = new Table<ImageStatCounter>(session, counterMapping, "image_stat_counters");
        imageStatCounterTable.CreateIfNotExists();
        this.statsService = statsService;
        this.objectService = objectService;
        this.leaderboardService = leaderboardService;
        this.eventBus = eventBus;
        this.skipService = skipService;
        this.multiplierService = multiplierService;
        this.wordService = wordService;
        this.streakService = streakService;
        this.privacyService = privacyService;
        this.expService = expService;
    }

    public async Task<UploadImageResponse> UploadFile(string label, Guid userId, IFormFile file, bool? licenseImage)
    {
        label = label.Trim();
        var fileName = file.FileName.Trim();
        if (!new FileExtensionContentTypeProvider().TryGetContentType(Path.GetFileName(fileName), out var contentType))
            contentType = "application/octet-stream";
        var existingCollection = imageTable.Where(i => i.ObjectLabel == label && i.UserId == userId).FirstOrDefault().ExecuteAsync();
        var currentTask = objectService.CurrentLabeltoCollect(userId);
        var objectTask = objectService.GetObject("en", label);
        var imageStatTask = imageStatCounterTable.Where(i => i.Label == label).FirstOrDefault().ExecuteAsync();
        var today = DateTime.UtcNow.DayOfYear + (DateTime.UtcNow.Year - 2020) * 365;
        var dailyRewardTask = GetRewardsFromDailyQuest(userId, today, label);
        var isNotFirstOfDay = streakService.HasCollectedAnyToday(userId);
        var privacy = await privacyService.GetConsent(userId);
        var newFile = new CapturedImage()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ObjectLabel = label,
            ContentType = contentType,
            Size = file.Length,
            Day = today
        };
        var infoTask = imageTable.Insert(newFile).ExecuteAsync();
        var route = $"{label.Replace(" ", "_")}/{newFile.Id}";
        logger.LogInformation($"Putting {route} to S3 bucket {bucketName}");
        if (privacy == null)
            throw new ApiException("privacy", "You need to have your privacy settings configured to upload images");
        Task? uploadTask = null;
        using var stream = new MemoryStream(); // stream has to be disposed after upload done
        if (privacy.NewService ?? false)
        {
            await file.CopyToAsync(stream);
            uploadTask = s3Client.PutObjectAsync(new PutObjectRequest()
            {
                ContentType = contentType,
                InputStream = stream,
                BucketName = bucketName,
                Key = route,
                DisablePayloadSigning = true
            });
        }
        List<Task> tasks = new List<Task>();
        var obj = await objectTask;
        var rewards = new UploadRewards();
        tasks.Add(statsService.IncreaseStat(userId, "images_uploaded"));
        var existing = await existingCollection;
        //if (obj != null && existing?.Day != today) // don't reward for the same object twice a day

        float value = obj?.Value ?? 0;
        rewards.ImageReward = obj?.Value ?? 0;
        if (await currentTask == label)
        {
            value *= 2;
            rewards.IsCurrent = true;
            tasks.Add(statsService.IncreaseStat(userId, "current_offset")); // tick current forward
        }
        else
        {
            tasks.Add(skipService.Collected(userId, label));
        }
        var multiplier = await multiplierService.GetMultipliers();
        var categoriesOfObject = await objectService.GetCategoriesForObject(label);
        await infoTask;
        var matchingMultiplier = multiplier.FirstOrDefault(m => categoriesOfObject.Contains(m.Category));
        (var dailyReward, var dailyQuestReward) = await dailyRewardTask;

        if (existing != null && dailyReward == 0)
            return new()
            {
                Image = newFile,
                Rewards = rewards
            };

        value += dailyQuestReward;
        rewards.DailyQuestReward = dailyQuestReward;
        if (matchingMultiplier != null)
        {
            value *= matchingMultiplier.Multiplier;
            rewards.Multiplier = matchingMultiplier.Multiplier;
        }
        value += dailyReward; // multiplier doesn't apply to daily reward
        if (obj == null)
        {
            if (await wordService.IsCollectableWord("en", label))
            {
                value += 200;
            }
            else
                value += 5;
        }
        rewards.DailyItemReward = dailyReward;
        var roundedValue = RounUpTo5(value);
        newFile.Metadata = new Dictionary<string, string>()
            {
                { "rewarded", roundedValue.ToString() },
                { "privacy.newService", (licenseImage ?? privacy.NewService ?? false).ToString()},
                { "privacy.resell", (licenseImage ?? privacy.AllowResell ?? false).ToString()},
                { "privacy.targeting", (privacy.TargetedAds ?? false).ToString()}
            };
        rewards.Total = roundedValue;
        tasks.Add(imageTable.Where(i => i.ObjectLabel == newFile.ObjectLabel && i.UserId == newFile.UserId && i.Day == newFile.Day)
                .Select(i => new CapturedImage() { Metadata = newFile.Metadata }).Update().ExecuteAsync());
        tasks.Add(UpdateExpScore(userId, roundedValue, label));
        if (existing == null)
            tasks.Add(statsService.IncreaseStat(userId, "unique_images_uploaded"));
        eventBus.OnImageUploaded(new ImageUploadEvent()
        {
            UserId = userId,
            Exp = roundedValue,
            ImageUrl = route,
            label = label,
            IsUnique = existing == null,
            ImageReward = rewards.ImageReward,
            IsCurrent = rewards.IsCurrent
        });
        if (obj?.Value > 10)
            await objectService.DecreaseValueTo("en", label, obj.Value -= 10);

        logger.LogInformation("user {userId} uploaded image {route} got rewarded with {rewards} {obj} {existing}", userId, route, JsonConvert.SerializeObject(rewards), JsonConvert.SerializeObject(obj), JsonConvert.SerializeObject(existing));
        if (uploadTask != null)
            await uploadTask;
        await Task.WhenAll(tasks);
        IncreaseImageUploadTimesCount(label);
        return new()
        {
            Image = newFile,
            Rewards = rewards,
            Stats = new()
            {
                ExtendedStreak = !await isNotFirstOfDay,
                CollectedTimes = (await imageStatTask)?.CollectCount ?? 1
            }
        };

        static int RounUpTo5(float value)
        {
            return ((int)(value + 0.1)) / 5 * 5;
        }
    }

    public async Task DeleteImage(Guid userId, Guid id)
    {
        var stored = await imageTable.Where(i => i.Id == id).FirstOrDefault().ExecuteAsync();
        if (stored == null)
        {
            throw new ApiException("not_found", "Image not found");
        }
        if (stored.UserId != userId)
        {
            throw new ApiException("forbidden", "You are not allowed to access this image");
        }
        await imageTable
            .Where(i => i.ObjectLabel == stored.ObjectLabel && i.UserId == stored.UserId && i.Day == stored.Day)
            .Delete().ExecuteAsync();
        await s3Client.DeleteObjectAsync(new DeleteObjectRequest()
        {
            BucketName = bucketName,
            Key = $"{stored.ObjectLabel.Replace(" ", "_")}/{id}"
        });
    }

    private void IncreaseImageUploadTimesCount(string label)
    {
        _ = Task.Run(() => imageStatCounterTable.Where(i => i.Label == label)
                    .Select(i => new ImageStatCounter() { CollectCount = 1 })
                    .Update().ExecuteAsync());
    }

    private async Task<(int itemReward, int querstReward)> GetRewardsFromDailyQuest(Guid userId, int today, string label)
    {
        var dailyTask = objectService.GetDailyLabels(userId);
        var dailyLabels = await dailyTask;
        if (!dailyLabels.TryGetValue(label, out var itemReward))
        {
            return (0, 0);
        }
        var dailyItemsCollected = await imageTable.Where(i => i.UserId == userId && i.Day == today && dailyLabels.Keys.Contains(i.ObjectLabel)).Select(i => i.ObjectLabel).ExecuteAsync();
        var alreadyCollected = dailyItemsCollected.Count();
        var reward = (int)Math.Pow(75, alreadyCollected / 2 + 1);
        return (itemReward, reward);
    }

    private async Task UpdateExpScore(Guid userId, long value, string label)
    {
        var expTask = expService.AddExp(userId, value, "image_upload", $"Uploading image of {label}", $"{label}-{DateTime.UtcNow.Date:yyyy-MM-dd}");
        var dailyStatTask = statsService.IncreaseExpireStat(DateTimeOffset.UtcNow, userId, "daily_exp", value);
        var lastDayOfWeek = DateTime.Now.RoundDown(TimeSpan.FromDays(7)).AddDays(7);
        var weeklyExpTask = statsService.IncreaseExpireStat(lastDayOfWeek, userId, "weekly_exp", value);
        await dailyStatTask;
        await weeklyExpTask;
        await expTask;
        var boardNames = new BoardNames();
        var dailyExpStat = await statsService.GetExpireStat(DateTimeOffset.UtcNow, userId, "daily_exp");
        await leaderboardService.SetScore(boardNames.DailyExp, userId, dailyExpStat);
        var weeklyExpStat = await statsService.GetExpireStat(lastDayOfWeek, userId, "weekly_exp");
        await leaderboardService.SetScore(boardNames.WeeklyExp, userId, weeklyExpStat);
    }

    public async Task<CapturedImage> AddDescription(Guid id, Guid userId, string description)
    {
        var stored = await imageTable.Where(i => i.Id == id).FirstOrDefault().ExecuteAsync();
        if (stored == null)
        {
            throw new ApiException("not_found", "Image not found");
        }
        if (stored.UserId != userId)
        {
            throw new ApiException("forbidden", "You are not allowed to access this image");
        }
        stored.Description = description;
        if (stored.Metadata?.TryGetValue("rewarded", out var value) ?? false)
        {
            Console.WriteLine($"Adding {value} exp to user {stored.UserId} for description");
            await statsService.IncreaseStat(stored.UserId, "exp", int.Parse(value));
        }
        await imageTable.Where(i => i.ObjectLabel == stored.ObjectLabel && i.UserId == stored.UserId && i.Day == stored.Day)
            .Select(i => new CapturedImage() { Description = description }).Update().ExecuteAsync();
        return stored;
    }

    public async Task<CapturedImageWithDownloadUrl> GetImage(string userId, Guid id)
    {
        var stored = await imageTable.Where(i => i.Id == id).FirstOrDefault().ExecuteAsync();
        if (stored == null)
        {
            return null;
        }
        if (userId != "admin" && stored.UserId != Guid.Parse(userId))
        {
            throw new ApiException("forbidden", "You are not allowed to access this image");
        }
        var url = await s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest()
        {
            BucketName = bucketName,
            Key = $"{stored.ObjectLabel.Replace(" ", "_")}/{id}",
            Expires = DateTime.UtcNow.AddMinutes(5)
        });
        return AddDownloadUrl(stored, url);
    }

    private static CapturedImageWithDownloadUrl AddDownloadUrl(CapturedImage stored, string url)
    {
        return new CapturedImageWithDownloadUrl()
        {
            Id = stored.Id,
            UserId = stored.UserId,
            ObjectLabel = stored.ObjectLabel,
            ContentType = stored.ContentType,
            Size = stored.Size,
            Description = stored.Description,
            Day = stored.Day,
            Metadata = stored.Metadata,
            Verifications = stored.Verifications,
            DownloadUrl = url
        };
    }
}

public class CapturedImageWithDownloadUrl : CapturedImage
{
    public string DownloadUrl { get; set; }
}

public class ImageStatCounter
{
    public string Label { get; set; }
    public long CollectCount { get; set; }
}
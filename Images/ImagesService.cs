using Amazon.S3;
using Amazon.S3.Model;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Core;
using Microsoft.AspNetCore.StaticFiles;
using ISession = Cassandra.ISession;


public class ImagesService
{
    private readonly ILogger<ImagesService> logger;
    private readonly IAmazonS3 s3Client;
    private readonly string bucketName;
    private readonly Table<CapturedImage> imageTable;
    private readonly StatsService statsService;
    private readonly ObjectService objectService;
    private readonly LeaderboardService leaderboardService;
    private readonly SkipService skipService;

    public ImagesService(ILogger<ImagesService> logger, IAmazonS3 s3Client, IConfiguration config, ISession session, StatsService statsService, ObjectService objectService, LeaderboardService leaderboardService)
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
        this.statsService = statsService;
        this.objectService = objectService;
        this.leaderboardService = leaderboardService;
    }

    public async Task<CapturedImage> UploadFile(string label, Guid userId, IFormFile file)
    {
        var fileName = file.FileName.Trim();
        if (!new FileExtensionContentTypeProvider().TryGetContentType(Path.GetFileName(fileName), out var contentType))
            contentType = "application/octet-stream";
        var newFile = new CapturedImage()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ObjectLabel = label,
            ContentType = contentType,
            Size = file.Length
        };
        var info = await imageTable.Insert(newFile).ExecuteAsync();
        var route = $"{label.Replace(" ", "_")}/{newFile.Id}";
        logger.LogInformation($"Putting {route} to S3 bucket {bucketName}");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        await s3Client.PutObjectAsync(new PutObjectRequest()
        {
            ContentType = contentType,
            InputStream = stream,
            BucketName = bucketName,
            Key = route,
            DisablePayloadSigning = true
        });
        var currentTask = objectService.CurrentLabeltoCollect(userId);
        await statsService.IncreaseStat(userId, "images_uploaded");
        var obj = await objectService.GetObject("en", label);
        if (obj != null)
        {
            var value = obj.Value;
            if (await currentTask == label)
            {
                value *= 2;
                await statsService.IncreaseStat(userId, "current_offset"); // tick current forward
            }
            else
            {
                await skipService.Collected(userId, label);
            }
            var statTask = statsService.IncreaseStat(userId, "exp", value);
            newFile.Metadata = new Dictionary<string, string>()
            {
                { "rewarded", value.ToString() }
            };
            await imageTable.Where(i => i.ObjectLabel == newFile.ObjectLabel && i.UserId == newFile.UserId && i.Day == newFile.Day)
                    .Select(i => new CapturedImage() { Metadata = newFile.Metadata }).Update().ExecuteAsync();
            await UpdateExpScore(userId);
            if (obj.Value > 10)
                await objectService.DecreaseValueTo("en", label, obj.Value -= 10);
            await statTask;
        }
        return newFile;
    }

    private async Task UpdateExpScore(Guid userId)
    {
        var expStat = await statsService.GetStat(userId, "exp");
        await leaderboardService.SetScore("exp_overall", userId, expStat);
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

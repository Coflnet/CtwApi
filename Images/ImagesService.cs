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
    private readonly Table<CapturedImage> categoryService;

    public ImagesService(ILogger<ImagesService> logger, IAmazonS3 s3Client, IConfiguration config, ISession session)
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
        categoryService = new Table<CapturedImage>(session, mapping, "named_images");
        categoryService.CreateIfNotExists();
        session.Execute("DROP TABLE IF EXISTS images");
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
        var info = await categoryService.Insert(newFile).ExecuteAsync();
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
        return newFile;
    }

    public async Task<CapturedImageWithDownloadUrl> GetImage(string userId, Guid id)
    {
        var stored = await categoryService.Where(i => i.Id == id).FirstOrDefault().ExecuteAsync();
        if (stored == null)
        {
            return null;
        }
        if(userId != "admin" && stored.UserId != Guid.Parse(userId))
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

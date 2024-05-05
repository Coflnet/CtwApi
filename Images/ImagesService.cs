using Amazon.S3;
using Amazon.S3.Model;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Microsoft.AspNetCore.StaticFiles;
using ISession = Cassandra.ISession;


public class ImagesService
{
    private readonly ILogger<ImagesService> logger;
    private readonly IAmazonS3 s3Client;
    private readonly string bucketName;
    private readonly Table<CapturedImage> categoryService;

    public ImagesService(ILogger<ImagesService> logger, IAmazonS3 s3Client, string bucketName, ISession session)
    {
        this.logger = logger;
        this.s3Client = s3Client;
        this.bucketName = bucketName;
        var mapping = new MappingConfiguration()
            .Define(new Map<CapturedImage>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.ObjectId)
            .ClusteringKey(t => t.Verifications)
            .Column(t => t.Id, cm => cm.WithSecondaryIndex())
            .Column(t => t.ObjectId, cm => cm.WithSecondaryIndex())
            .Column(t => t.ContentType)
            .Column(t => t.Size)
        );
        categoryService = new Table<CapturedImage>(session, mapping, "images");
        categoryService.CreateIfNotExists();
    }

    public async Task<CapturedImage> UploadFile(Guid objectId, Guid userId, IFormFile file)
    {
        var fileName = file.FileName.Trim();
        if (!new FileExtensionContentTypeProvider().TryGetContentType(Path.GetFileName(fileName), out var contentType))
            contentType = "application/octet-stream";
        var newFile = new CapturedImage()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ObjectId = objectId,
            ContentType = contentType,
            Size = file.Length
        };
        var info = await categoryService.Insert(newFile).ExecuteAsync();
        var route = $"{newFile.Id}";
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
}

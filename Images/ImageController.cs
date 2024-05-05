using Coflnet.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Coflnet.Auth;

[ApiController]
[Route("api")]
public class ImageController : ControllerBase
{
    private readonly ImagesService imageService;
    private readonly ILogger<ImageController> logger;

    public ImageController(ImagesService imageService, ILogger<ImageController> logger)
    {
        this.imageService = imageService;
        this.logger = logger;
    }

    [HttpPost("images/{objectId}"), DisableRequestSizeLimit]
    [Authorize]
    public async Task<CapturedImage> UploadImage(Guid objectId)
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub").Value);
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            throw new ApiException("missing_upload", "No file uploaded, an image is required.");
        }
        return await imageService.UploadFile(objectId, userId, file);
    }
}
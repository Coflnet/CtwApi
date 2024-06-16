using Coflnet.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

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

    /// <summary>
    /// Upload an image
    /// NOTE: the image has to be included as form-data file
    /// </summary>
    /// <param name="label"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    [HttpPost("images/{label}"), DisableRequestSizeLimit]
    [Authorize]
    [SwaggerOperation(OperationId = "ApiFileUpload.UploadFile", Summary = "Upload an image", Description = "Upload an image")]
    public async Task<UploadImageResponse> UploadImage(string label)
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            throw new ApiException("missing_upload", "No file uploaded, an image is required.");
        }
        return await imageService.UploadFile(label, userId, file);
    }

    [HttpPost("images/{id}/description")]
    [Authorize]
    public async Task<CapturedImage> AddDescription(Guid id, [FromBody] string description)
    {
        var userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "sub").Value);
        return await imageService.AddDescription(id, userId, description);
    }

    /// <summary>
    /// Get image metadata with download url
    /// </summary>
    /// <param name="id"></param>
    /// <response code="200" cref="CapturedImageWithDownloadUrl">Url</response>
    /// <returns></returns>
    [HttpGet("images/{id}")]
    [Authorize]
    [SwaggerResponse(200, type: typeof(CapturedImageWithDownloadUrl))]
    [SwaggerResponse(404, "Image not found")]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var image = await imageService.GetImage(User.Claims.FirstOrDefault(c => c.Type == "sub").Value, id);
        if (image == null)
        {
            return NotFound();
        }
        return Ok(image);
    }
}
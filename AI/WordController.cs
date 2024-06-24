using Coflnet.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class WordController : ControllerBase
{
    private WordService wordService;

    public WordController(WordService wordService)
    {
        this.wordService = wordService;
    }

    [HttpGet("isCollectableWord")]
    [Authorize]
    public async Task<object> IsCollectableWord(string locale, string phrase)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims");
        if (userId != "admin")
        {
            throw new ApiException("not_admin", "Only admins can check if a word is collectable");
        }
        var result = await wordService.IsCollectableWordExplanation(locale, phrase);
        return new { IsObject = result.Item1, Explanation = result.Item2 };
    }
}

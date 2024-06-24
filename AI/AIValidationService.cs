using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using Newtonsoft.Json;

public class AIValidationService
{
    private IOpenAIService openAIService;
    private ILogger<AIValidationService> logger;

    public AIValidationService(IOpenAIService openAIService, ILogger<AIValidationService> logger)
    {
        this.openAIService = openAIService;
        openAIService.SetDefaultModelId("gpt-3.5-turbo");
        this.logger = logger;
    }

    public async Task<WordService.Word?> IsCollectableWord(string locale, string phrase)
    {
        var prompt = $"""
        Answer these questions about "{phrase}" with yes or no in the form of (yes,yes,yes,yes,yes,iso code,category)
        Is it an object in the real world?
        Can you photograph it? 
        Is it an abbreviation or unspecific? 
        Is it a name of a person,city or company?
        Is it a product?
        What language is it in as 2 digit code?
        What category would fit it in?
        """;
        var response = await openAIService.ChatCompletion.CreateCompletion(new OpenAI.ObjectModels.RequestModels.ChatCompletionCreateRequest()
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<ChatMessage>()
            {
                ChatMessage.FromUser(prompt)
            },
            Stop = ")"
        });
        logger.LogInformation($"AI response for {phrase} is {JsonConvert.SerializeObject(response, Formatting.Indented)}");
        var split = response.Choices[0].Message.Content?.TrimStart('(').Split(",");
        if (split == null || split.Length != 7)
        {
            return null;
        }
        return new WordService.Word()
        {
            Locale = locale,
            Phrase = phrase,
            IsRealItem = split[0].Trim().ToLower() == "yes",
            CanMakePicture = split[1].Trim().ToLower() == "yes",
            IsAbbreviation = split[2].Trim().ToLower() == "yes",
            IsPersonCityOrCompany = split[3].Trim().ToLower() == "yes",
            IsProduct = split[4].Trim().ToLower() == "yes",
            LocaleGuess = split[5].Trim().ToLower(),
            Category = split[6].Trim()
        };
    }
}

using Coflnet.Ctw.Api.Client.Api;
using Coflnet.Ctw.Api.Client.Client;
using FluentAssertions;
using Newtonsoft.Json;

namespace Test;

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        Configuration configuration = GetConfigWithAuth();
        var leaderboardApi = new LeaderboardApi(configuration);
        await leaderboardApi.SetProfileAsync(new () { Name = "Georg", Avatar = "test" });
        var privacyApi = new PrivacyApi(configuration);
        await privacyApi.SaveConsentAsync(new () {  });
        var imageApi = new ImageApi(configuration);
        using var pixelFileStream = new FileStream("pixel.jpg", FileMode.Open);
        var result = await imageApi.UploadImageWithHttpInfoAsync("Document Scanner", pixelFileStream);
        Console.WriteLine($"Upload status: {result.StatusCode}");
        Console.WriteLine($"Upload result: {JsonConvert.SerializeObject(result.Data)}");
        Assert.NotNull(result);
        Assert.NotNull(result.Data.Image.ObjectLabel);
        result.Data.Rewards.BaseReward.Should().BeGreaterThan(5);

        var statsApi = new StatsApi(configuration);
        var stats = await statsApi.GetAllStatsAsync();
        Console.WriteLine($"Stats: {JsonConvert.SerializeObject(stats)}");
        Assert.NotNull(stats);
        Assert.NotEmpty(stats);
        Assert.Contains(stats, s => s.StatName == "images_uploaded" && s.Value > 0);
        // user should have no more than 2 skips
        Assert.Contains(stats, s => s.StatName == "skips_available" && s.Value == 2);

        var objectApi = new ObjectApi(configuration);
        var current = await objectApi.GetNextObjectAsync();
    }

    private static Configuration GetConfigWithAuth()
    {
        var authClient = new AuthApi("http://localhost:5122");
        var userToken = authClient.Login(new() { Secret = "stringstringstringstringstringstring", Locale = "en" });
        var bearerToken = userToken.Token;
        var configuration = new Configuration
        {
            BasePath = "http://localhost:5122",
            ApiKey = new Dictionary<string, string>() { { "Authorization", bearerToken } },
            ApiKeyPrefix = new Dictionary<string, string>() { { "Authorization", "Bearer" } }
        };
        return configuration;
    }
}
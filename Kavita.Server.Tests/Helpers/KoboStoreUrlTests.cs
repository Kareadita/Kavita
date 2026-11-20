using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Http;

namespace Kavita.Server.Tests.Helpers;

public class KoboStoreUrlTests
{
    [Theory]
    [InlineData("/kobo/KEY//evil.example/steal", "", "https://storeapi.kobo.com/evil.example/steal")]
    [InlineData("/kobo/a-route-segment-longer-than-the-header-key//evil.example/steal", "", "https://storeapi.kobo.com/evil.example/steal")]
    [InlineData("/kobo/KEY///evil.example/steal", "", "https://storeapi.kobo.com/evil.example/steal")]
    [InlineData("/kobo/KEY/\\evil.example/steal", "", "https://storeapi.kobo.com/%5Cevil.example/steal")]
    [InlineData("/kobo/KEY/https://evil.example/steal", "", "https://storeapi.kobo.com/https://evil.example/steal")]
    [InlineData("/kobo/KEY/@evil.example/steal", "", "https://storeapi.kobo.com/@evil.example/steal")]
    [InlineData("/kobo/KEY/a?b#c", "", "https://storeapi.kobo.com/a%3Fb%23c")]
    [InlineData("/kobo/KEY", "", "https://storeapi.kobo.com/")]
    [InlineData("/kobo/KEY/", "", "https://storeapi.kobo.com/")]
    [InlineData("/kobo/KEY/v1/library/sync", "?x=1", "https://storeapi.kobo.com/v1/library/sync?x=1")]
    [InlineData("/Kobo/KEY/v1/user/reviews", "?ProductIds=a&ProductIds=b", "https://storeapi.kobo.com/v1/user/reviews?ProductIds=a&ProductIds=b")]
    [InlineData("/kobo/KEY/v1/search", "?q=a%20b%26c", "https://storeapi.kobo.com/v1/search?q=a%20b%26c")]
    [InlineData("/kobo/KEY/v1/x", "?apiKey=KEY", "https://storeapi.kobo.com/v1/x")]
    [InlineData("/kobo/KEY/v1/x", "?a=1&APIKEY=KEY&b=2", "https://storeapi.kobo.com/v1/x?a=1&b=2")]
    [InlineData("/kobo/KEY/v1/x", "?api%4Bey=KEY&b=2", "https://storeapi.kobo.com/v1/x?b=2")]
    [InlineData("/kobo/KEY/v1/x", "?apiKeyish=1", "https://storeapi.kobo.com/v1/x?apiKeyish=1")]
    public void Build_PinsHost_AndDropsApiKey(string path, string query, string expected)
    {
        var url = KoboStoreUrl.Build(new PathString(path), new QueryString(query));

        Assert.Equal("storeapi.kobo.com", url.Host);
        Assert.Equal(expected, url.AbsoluteUri);
    }

    [Fact]
    public void Build_WithoutKoboPrefix_KeepsFullPath()
    {
        var url = KoboStoreUrl.Build(new PathString("/api/v3/content/8/annotations"), new QueryString(""));

        Assert.Equal("https://storeapi.kobo.com/api/v3/content/8/annotations", url.AbsoluteUri);
    }

    [Fact]
    public void Build_WithBaseAddress_UsesThatHost()
    {
        var url = KoboStoreUrl.Build(new PathString("/api/v3/content/8/annotations"), new QueryString(""),
            new Uri("https://readingservices.kobo.com"));

        Assert.Equal("https://readingservices.kobo.com/api/v3/content/8/annotations", url.AbsoluteUri);
    }
}

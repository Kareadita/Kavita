using System.Text.Json.Nodes;
using Kavita.Services.Kobo;
using Xunit;

namespace Kavita.Services.Tests.Kobo;

public class KoboReadingStateMapperTests
{
    [Fact]
    public void ResolvePagesRead_Finished_OverridesProgressPercentBelow100()
    {
        var readingState = new JsonObject
        {
            ["CurrentBookmark"] = new JsonObject { ["ProgressPercent"] = 85 },
            ["StatusInfo"] = new JsonObject { ["Status"] = "Finished" },
        };

        Assert.Equal(10, KoboReadingStateMapper.ResolvePagesRead(readingState, 10, existingPagesRead: 3));
    }

    [Fact]
    public void ResolvePagesRead_Finished_WithoutPercent_ReturnsTotalPages()
    {
        var readingState = new JsonObject
        {
            ["StatusInfo"] = new JsonObject { ["Status"] = "Finished" },
        };

        Assert.Equal(10, KoboReadingStateMapper.ResolvePagesRead(readingState, 10, existingPagesRead: 0));
    }

    [Fact]
    public void ResolvePagesRead_Reading_UsesProgressPercent()
    {
        var readingState = new JsonObject
        {
            ["CurrentBookmark"] = new JsonObject { ["ProgressPercent"] = 40 },
            ["StatusInfo"] = new JsonObject { ["Status"] = "Reading" },
        };

        Assert.Equal(4, KoboReadingStateMapper.ResolvePagesRead(readingState, 10, existingPagesRead: 0));
    }
}

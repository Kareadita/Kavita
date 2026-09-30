using System.Text.Json;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Tests.DTOs.SignalR;

public class MessageFactoryTests
{
    // Same shape SignalR's JSON hub protocol writes: camelCase, enums as numbers, NaN rejected
    private static readonly JsonSerializerOptions HubJson = new(JsonSerializerDefaults.Web);

    private static JsonElement Serialize(SignalRMessage message) =>
        JsonSerializer.SerializeToElement(message, HubJson);

    public static TheoryData<SignalRMessage, MessageEventPriority> PriorityCases => new()
    {
        { MessageFactory.FileScanProgressEvent("M:/Manga/One Piece", "Manga", ProgressEventType.Updated), MessageEventPriority.Activity },
        { MessageFactory.LibraryScanProgressEvent("Manga", ProgressEventType.Updated, "One Piece", 138, 180), MessageEventPriority.Activity },
        { MessageFactory.BackupDatabaseProgressEvent(0.5f), MessageEventPriority.Activity },
        { MessageFactory.ErrorEvent("Comics scan aborted", "Some root folders are empty"), MessageEventPriority.Error },
        { MessageFactory.ExternalMatchRateLimitErrorEvent(1, "Vinland Saga"), MessageEventPriority.Error },
        { MessageFactory.InfoEvent("Scan library task delayed", "Rescheduled"), MessageEventPriority.Info },
        { MessageFactory.ScrobblingKeyExpiredEvent(ScrobbleProvider.AniList), MessageEventPriority.Action },
        { MessageFactory.SeriesAddedEvent(1, "One Piece", 1), MessageEventPriority.Silent },
        { MessageFactory.LibraryModifiedEvent(1, "update"), MessageEventPriority.Silent },
    };

    [Theory]
    [MemberData(nameof(PriorityCases))]
    public void Factory_SetsPriority(SignalRMessage message, MessageEventPriority expected)
    {
        Assert.Equal(expected, message.Priority);
    }

    [Fact]
    public void Serialize_WritesPriorityAsNumber_AndEventTimeUtc()
    {
        var json = Serialize(MessageFactory.FileScanProgressEvent("M:/Manga/One Piece", "Manga", ProgressEventType.Started));

        Assert.Equal(JsonValueKind.Number, json.GetProperty("priority").ValueKind);
        Assert.Equal((int) MessageEventPriority.Activity, json.GetProperty("priority").GetInt32());

        var eventTime = json.GetProperty("eventTimeUtc").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, eventTime.Kind);
        Assert.InRange(eventTime, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Serialize_KeepsExistingEnvelopeFields()
    {
        var json = Serialize(MessageFactory.FileScanProgressEvent("M:/Manga/One Piece", "Manga", ProgressEventType.Started));

        Assert.Equal("FileScanProgress", json.GetProperty("name").GetString());
        Assert.Equal("Scanning Manga", json.GetProperty("title").GetString());
        Assert.Equal("M:/Manga/One Piece", json.GetProperty("subTitle").GetString());
        Assert.Equal("started", json.GetProperty("eventType").GetString());
        Assert.Equal("indeterminate", json.GetProperty("progress").GetString());
    }

    [Fact]
    public void ConvertCoverProgress_HasItsOwnName()
    {
        var covers = MessageFactory.ConvertCoverProgressEvent(0.5f, ProgressEventType.Updated);
        var bookmarks = MessageFactory.ConvertBookmarksProgressEvent(0.5f, ProgressEventType.Updated);

        Assert.Equal("ConvertCoversProgress", covers.Name);
        Assert.NotEqual(bookmarks.Name, covers.Name);
    }

    [Fact]
    public void SmartCollectionProgress_ZeroTotal_IsZeroAndSerializes()
    {
        var message = MessageFactory.SmartCollectionProgressEvent("Seasonal", string.Empty, 0, 0, ProgressEventType.Started);

        var json = Serialize(message);

        Assert.Equal(0f, json.GetProperty("body").GetProperty("progress").GetSingle());
    }

    [Theory]
    [InlineData(18, 40, 0.45f)]
    [InlineData(40, 40, 1f)]
    [InlineData(45, 40, 1f)]
    public void SmartCollectionProgress_IsRatioClampedToOne(int current, int total, float expected)
    {
        var json = Serialize(MessageFactory.SmartCollectionProgressEvent("Seasonal", "Frieren", current, total, ProgressEventType.Updated));

        Assert.Equal(expected, json.GetProperty("body").GetProperty("progress").GetSingle(), 3);
    }

    [Fact]
    public void FileScanProgress_WithCounts_IsDeterminate()
    {
        var json = Serialize(MessageFactory.FileScanProgressEvent("M:/Half & Half", "Manga", ProgressEventType.Updated,
            MessageEventCode.ScanListingFolders, 812, 1496));
        var body = json.GetProperty("body");

        Assert.Equal("determinate", json.GetProperty("progress").GetString());
        Assert.Equal("scan-listing-folders", json.GetProperty("code").GetString());
        Assert.Equal(812, body.GetProperty("current").GetInt32());
        Assert.Equal(1496, body.GetProperty("total").GetInt32());
        Assert.Equal(812 / 1496f, body.GetProperty("progress").GetSingle(), 4);
    }

    [Fact]
    public void FileScanProgress_WithoutCounts_StaysIndeterminate()
    {
        var json = Serialize(MessageFactory.FileScanProgressEvent("File Scan Starting", "Manga", ProgressEventType.Started));

        Assert.Equal("indeterminate", json.GetProperty("progress").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("body").GetProperty("progress").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("code").ValueKind);
    }

    [Fact]
    public void LibraryScanProgress_HasProcessingSeriesCode()
    {
        var message = MessageFactory.LibraryScanProgressEvent("Manga", ProgressEventType.Updated, "One Piece", 27, 28);

        Assert.Equal(MessageEventCode.ScanProcessingSeries, message.Code);
    }

    [Fact]
    public void LibraryModified_BodyUsesLibraryId()
    {
        var body = Serialize(MessageFactory.LibraryModifiedEvent(7, "delete")).GetProperty("body");

        Assert.Equal(7, body.GetProperty("libraryId").GetInt32());
        Assert.False(body.TryGetProperty("librayId", out _));
    }
}

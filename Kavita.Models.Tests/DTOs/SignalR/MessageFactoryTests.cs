using System.Text.Json;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.Update;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Tests.DTOs.SignalR;

public class MessageFactoryTests
{
    // Same shape SignalR's JSON hub protocol writes: camelCase, enums as numbers, NaN rejected
    private static readonly JsonSerializerOptions HubJson = new(JsonSerializerDefaults.Web);

    private static JsonElement Serialize(SignalRMessageDto message) =>
        JsonSerializer.SerializeToElement(message, HubJson);

    public static TheoryData<SignalRMessageDto, MessageEventPriority> PriorityCases => new()
    {
        { MessageFactory.FileScanProgressEvent("M:/Manga/One Piece", 1, "Manga", ProgressEventType.Updated), MessageEventPriority.Activity },
        { MessageFactory.LibraryScanProgressEvent(1, "Manga", ProgressEventType.Updated, "One Piece", 138, 180), MessageEventPriority.Activity },
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
    public void Factory_SetsPriority(SignalRMessageDto message, MessageEventPriority expected)
    {
        Assert.Equal(expected, message.Priority);
    }

    [Fact]
    public void Serialize_WritesPriorityAsNumber_AndEventTimeUtc()
    {
        var json = Serialize(MessageFactory.FileScanProgressEvent("M:/Manga/One Piece", 1, "Manga", ProgressEventType.Started));

        Assert.Equal(JsonValueKind.Number, json.GetProperty("priority").ValueKind);
        Assert.Equal((int) MessageEventPriority.Activity, json.GetProperty("priority").GetInt32());

        var eventTime = json.GetProperty("eventTimeUtc").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, eventTime.Kind);
        Assert.InRange(eventTime, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Serialize_KeepsExistingEnvelopeFields()
    {
        var json = Serialize(MessageFactory.FileScanProgressEvent("M:/Manga/One Piece", 1, "Manga", ProgressEventType.Started));

        Assert.Equal("FileScanProgress", json.GetProperty("name").GetString());
        Assert.Equal("Scanning Manga", json.GetProperty("title").GetString());
        Assert.Equal("M:/Manga/One Piece", json.GetProperty("subTitle").GetString());
        Assert.Equal("started", json.GetProperty("eventType").GetString());
        Assert.Equal("indeterminate", json.GetProperty("progress").GetString());
    }

    public static TheoryData<SignalRMessageDto> SingleCases => new()
    {
        MessageFactory.InfoEvent("Scan library task delayed", "Rescheduled"),
        MessageFactory.ErrorEvent("Comics scan aborted", "Some root folders are empty"),
        MessageFactory.WordCountFailedEvent(2, 42, "Frieren", "B:/Frieren/Frieren v01.epub"),
        MessageFactory.UpdateVersionEvent(new UpdateNotificationDto
        {
            CurrentVersion = "0.9.1.0",
            UpdateVersion = "0.9.2.0",
            UpdateBody = string.Empty,
            UpdateTitle = "v0.9.2",
            UpdateUrl = string.Empty,
            PublishDate = string.Empty,
        }),
    };

    [Theory]
    [MemberData(nameof(SingleCases))]
    public void OneOffEvents_SerializeAsSingle(SignalRMessageDto message)
    {
        Assert.Equal("single", Serialize(message).GetProperty("eventType").GetString());
    }

    [Fact]
    public void LibraryScanProgress_NoneLeft_IsComplete()
    {
        var json = Serialize(MessageFactory.LibraryScanProgressEvent(1, "Manga", ProgressEventType.Updated, string.Empty, 0, 28));

        Assert.Equal("updated", json.GetProperty("eventType").GetString());
        Assert.Equal(1f, json.GetProperty("body").GetProperty("progress").GetSingle());
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
        var json = Serialize(MessageFactory.FileScanProgressEvent("M:/Half & Half", 1, "Manga", ProgressEventType.Updated,
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
        var json = Serialize(MessageFactory.FileScanProgressEvent("File Scan Starting", 1, "Manga", ProgressEventType.Started));

        Assert.Equal("indeterminate", json.GetProperty("progress").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("body").GetProperty("progress").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("code").ValueKind);
    }

    [Fact]
    public void LibraryScanProgress_HasProcessingSeriesCode()
    {
        var message = MessageFactory.LibraryScanProgressEvent(1, "Manga", ProgressEventType.Updated, "One Piece", 27, 28);

        Assert.Equal(MessageEventCode.ScanProcessingSeries, message.Code);
    }

    [Fact]
    public void LibraryModified_BodyUsesLibraryId()
    {
        var body = Serialize(MessageFactory.LibraryModifiedEvent(7, "delete")).GetProperty("body");

        Assert.Equal(7, body.GetProperty("libraryId").GetInt32());
        Assert.False(body.TryGetProperty("librayId", out _));
    }

    [Fact]
    public void ScanProgressEvents_CarryLibraryId()
    {
        var fileScan = Serialize(MessageFactory.FileScanProgressEvent("M:/One Piece", 4, "Manga", ProgressEventType.Updated)).GetProperty("body");
        var libraryScan = Serialize(MessageFactory.LibraryScanProgressEvent(4, "Manga", ProgressEventType.Updated, "One Piece", 1, 2)).GetProperty("body");

        Assert.Equal(4, fileScan.GetProperty("libraryId").GetInt32());
        Assert.Equal("Manga", fileScan.GetProperty("libraryName").GetString());
        Assert.Equal(4, libraryScan.GetProperty("libraryId").GetInt32());
    }

    [Fact]
    public void CodedError_HasTypedBodyFields_AndKeepsFallbackText()
    {
        var message = MessageFactory.WordCountFailedEvent(2, 42, "Frieren", "B:/Frieren/Frieren v01.epub");

        var json = Serialize(message);
        var body = json.GetProperty("body");

        Assert.Equal(MessageFactory.Error, message.Name);
        Assert.Equal(MessageEventPriority.Error, message.Priority);
        Assert.Equal("word-count-failed", json.GetProperty("code").GetString());
        Assert.Equal("There was an issue counting words on an epub", json.GetProperty("title").GetString());
        Assert.Equal("Frieren - B:/Frieren/Frieren v01.epub", json.GetProperty("subTitle").GetString());
        Assert.Equal("Error", body.GetProperty("name").GetString());
        Assert.Equal("There was an issue counting words on an epub", body.GetProperty("title").GetString());
        Assert.Equal(2, body.GetProperty("libraryId").GetInt32());
        Assert.Equal(42, body.GetProperty("seriesId").GetInt32());
        Assert.Equal("Frieren", body.GetProperty("seriesName").GetString());
        Assert.Equal("B:/Frieren/Frieren v01.epub", body.GetProperty("filePath").GetString());
        Assert.False(body.TryGetProperty("params", out _));
    }

    [Fact]
    public void RootFoldersInaccessible_SendsFoldersAsArray()
    {
        var message = MessageFactory.RootFoldersInaccessibleEvent(1, "Manga", ["M:/", "N:/"]);
        var body = Serialize(message).GetProperty("body");

        Assert.Equal("M:/, N:/", message.SubTitle);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("folders").ValueKind);
        Assert.Equal(2, body.GetProperty("folders").GetArrayLength());
    }

    [Fact]
    public void InfoEvent_WithoutCode_IsUnchanged()
    {
        var json = Serialize(MessageFactory.InfoEvent("Scan library task delayed", "Rescheduled"));
        var body = json.GetProperty("body");

        Assert.Equal(JsonValueKind.Null, json.GetProperty("code").ValueKind);
        Assert.Equal("Info", body.GetProperty("name").GetString());
        Assert.Equal("Rescheduled", body.GetProperty("subTitle").GetString());
    }

    [Fact]
    public void ScanSeriesDelayed_CarriesScheduleAndIds()
    {
        var runAt = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc);

        var message = MessageFactory.ScanSeriesDelayedEvent(1, 42, "Frieren", runAt);
        var json = Serialize(message);
        var body = json.GetProperty("body");

        Assert.Equal(MessageFactory.Info, message.Name);
        Assert.Equal("scan-series-delayed", json.GetProperty("code").GetString());
        Assert.Equal(runAt, body.GetProperty("scheduledForUtc").GetDateTime());
        Assert.Equal(1, body.GetProperty("libraryId").GetInt32());
        Assert.Equal(42, body.GetProperty("seriesId").GetInt32());
        Assert.Equal("Frieren", body.GetProperty("seriesName").GetString());
    }

    [Fact]
    public void ScrobblingKeyExpired_BodyHasProvider()
    {
        var body = Serialize(MessageFactory.ScrobblingKeyExpiredEvent(ScrobbleProvider.AniList)).GetProperty("body");

        Assert.Equal((int) ScrobbleProvider.AniList, body.GetProperty("provider").GetInt32());
    }

    [Fact]
    public void SmartCollectionProgress_BodyHasCollectionName()
    {
        var body = Serialize(MessageFactory.SmartCollectionProgressEvent("Seasonal", "Frieren", 1, 4, ProgressEventType.Updated)).GetProperty("body");

        Assert.Equal("Seasonal", body.GetProperty("collectionName").GetString());
    }
}

using System;
using API.DTOs.Update;
using API.Extensions;

namespace API.SignalR;

public static class MessageFactoryEntityTypes
{
    public const string Library = "library";
    public const string Series = "series";
    public const string Volume = "volume";
    public const string Chapter = "chapter";
    public const string CollectionTag = "collection";
    public const string ReadingList = "readingList";
}
public static class MessageFactory
{
    /// <summary>
    /// An update is available for the Kavita instance
    /// </summary>
    public const string UpdateAvailable = "UpdateAvailable";
    /// <summary>
    /// Used to tell when a scan series completes. This also informs UI to update series metadata
    /// </summary>
    public const string ScanSeries = "ScanSeries";
    /// <summary>
    /// Event sent out during Refresh Metadata for progress tracking
    /// </summary>
    private const string CoverUpdateProgress = "CoverUpdateProgress";
    /// <summary>
    /// Series is added to server
    /// </summary>
    public const string SeriesAdded = "SeriesAdded";
    /// <summary>
    /// Series is removed from server
    /// </summary>
    public const string SeriesRemoved = "SeriesRemoved";
    /// <summary>
    /// When a user is connects/disconnects from server
    /// </summary>
    public const string OnlineUsers = "OnlineUsers";
    /// <summary>
    /// When a series is added to a collection
    /// </summary>
    public const string SeriesAddedToCollection = "SeriesAddedToCollection";
    /// <summary>
    /// Event sent out during backing up the database
    /// </summary>
    private const string BackupDatabaseProgress = "BackupDatabaseProgress";
    /// <summary>
    /// Event sent out during cleaning up temp and cache folders
    /// </summary>
    private const string CleanupProgress = "CleanupProgress";
    /// <summary>
    /// Event sent out during downloading of files
    /// </summary>
    private const string DownloadProgress = "DownloadProgress";
    /// <summary>
    /// A cover was updated
    /// </summary>
    public const string CoverUpdate = "CoverUpdate";
    /// <summary>
    /// A custom site theme was removed or added
    /// </summary>
    private const string SiteThemeProgress = "SiteThemeProgress";
    /// <summary>
    /// A custom book theme was removed or added
    /// </summary>
    private const string BookThemeProgress = "BookThemeProgress";
    /// <summary>
    /// A type of event that has progress (determinate or indeterminate).
    /// The underlying event will have a name to give details on how to handle.
    /// </summary>
    /// <remarks>This is not an Event Name, it is used as the method only</remarks>
    public const string NotificationProgress = "NotificationProgress";
    /// <summary>
    /// Event sent out when Scan Loop is parsing a file
    /// </summary>
    private const string FileScanProgress = "FileScanProgress";
    /// <summary>
    /// A generic error that can occur in background processing
    /// </summary>
    public const string Error = "Error";
    /// <summary>
    /// When DB updates are occuring during a library/series scan
    /// </summary>
    private const string ScanProgress = "ScanProgress";
    /// <summary>
    /// When a library is created/deleted in the Server
    /// </summary>
    public const string LibraryModified = "LibraryModified";
    /// <summary>
    /// A user's progress was modified
    /// </summary>
    public const string UserProgressUpdate = "UserProgressUpdate";
    /// <summary>
    /// A user's account or preferences were updated and UI needs to refresh to stay in sync
    /// </summary>
    public const string UserUpdate = "UserUpdate";
    /// <summary>
    /// When bulk bookmarks are being converted
    /// </summary>
    private const string ConvertBookmarksProgress = "ConvertBookmarksProgress";
    /// <summary>
    /// When bulk covers are being converted
    /// </summary>
    private const string ConvertCoversProgress = "ConvertBookmarksProgress";
    /// <summary>
    /// When files are being scanned to calculate word count
    /// </summary>
    private const string WordCountAnalyzerProgress = "WordCountAnalyzerProgress";
    /// <summary>
    /// A generic message that can occur in background processing to inform user, but no direct action is needed
    /// </summary>
    public const string Info = "Info";
    /// <summary>
    /// When files are being emailed to a device
    /// </summary>
    public const string SendingToDevice = "SendingToDevice";


    public static SignalRMessage ScanSeriesEvent(int libraryId, int seriesId, string seriesName)
    {
        return new SignalRMessage()
        {
            Name = ScanSeries,
            EventType = ProgressEventType.Single,
            Body = new
            {
                LibraryId = libraryId,
                SeriesId = seriesId,
                SeriesName = seriesName
            }
        };
    }

    public static SignalRMessage SeriesAddedEvent(int seriesId, string seriesName, int libraryId)
    {
        return new SignalRMessage()
        {
            Name = SeriesAdded,
            Body = new
            {
                SeriesId = seriesId,
                SeriesName = seriesName,
                LibraryId = libraryId
            }
        };
    }

    public static SignalRMessage SeriesRemovedEvent(int seriesId, string seriesName, int libraryId)
    {
        return new SignalRMessage()
        {
            Name = SeriesRemoved,
            Body = new
            {
                SeriesId = seriesId,
                SeriesName = seriesName,
                LibraryId = libraryId
            }
        };
    }


    public static SignalRMessage WordCountAnalyzerProgressEvent(int libraryId, float progress, string eventType, string subtitle = "")
    {
        return new SignalRMessage()
        {
            Name = WordCountAnalyzerProgress,
            Title = "Analyzing Word count",
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Determinate,
            Body = new
            {
                LibraryId = libraryId,
                Progress = progress,
                EventTime = DateTime.Now
            }
        };
    }

    public static SignalRMessage CoverUpdateProgressEvent(int libraryId, float progress, string eventType, string subtitle = "")
    {
        return new SignalRMessage()
        {
            Name = CoverUpdateProgress,
            Title = "Refreshing Covers",
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Determinate,
            Body = new
            {
                LibraryId = libraryId,
                Progress = progress,
                EventTime = DateTime.Now
            }
        };
    }

    public static SignalRMessage BackupDatabaseProgressEvent(float progress, string subtitle = "")
    {
        return new SignalRMessage()
        {
            Name = BackupDatabaseProgress,
            Title = "Backing up Database",
            SubTitle = subtitle,
            EventType = progress switch
            {
                0f => "started",
                1f => "ended",
                _ => "updated"
            },
            Progress = ProgressType.Determinate,
            Body = new
            {
                Progress = progress
            }
        };
    }
    public static SignalRMessage CleanupProgressEvent(float progress, string subtitle = "")
    {
        return new SignalRMessage()
        {
            Name = CleanupProgress,
            Title = "Performing Cleanup",
            SubTitle = subtitle,
            EventType = progress switch
            {
                0f => "started",
                1f => "ended",
                _ => "updated"
            },
            Progress = ProgressType.Determinate,
            Body = new
            {
                Progress = progress
            }
        };
    }


    public static SignalRMessage UpdateVersionEvent(UpdateNotificationDto update)
    {
        return new SignalRMessage
        {
            Name = UpdateAvailable,
            Title = "Update Available",
            SubTitle = update.UpdateTitle,
            EventType = ProgressEventType.Single,
            Progress = ProgressType.None,
            Body = update
        };
    }

    public static SignalRMessage SendingToDeviceEvent(string subtitle, string eventType)
    {
        return new SignalRMessage
        {
            Name = SendingToDevice,
            Title = "Sending files to Device",
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Indeterminate,
            Body = new { }
        };
    }

    public static SignalRMessage SeriesAddedToCollectionEvent(int tagId, int seriesId)
    {
        return new SignalRMessage
        {
            Name = SeriesAddedToCollection,
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                TagId = tagId,
                SeriesId = seriesId
            }
        };
    }


    public static SignalRMessage ErrorEvent(string title, string subtitle)
    {
        return new SignalRMessage
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                Title = title,
                SubTitle = subtitle,
            }
        };
    }

    public static SignalRMessage InfoEvent(string title, string subtitle)
    {
        return new SignalRMessage
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                Title = title,
                SubTitle = subtitle,
            }
        };
    }

    public static SignalRMessage LibraryModifiedEvent(int libraryId, string action)
    {
        return new SignalRMessage
        {
            Name = LibraryModified,
            Title = "Library modified",
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                LibrayId = libraryId,
                Action = action,
            }
        };
    }

    public static SignalRMessage DownloadProgressEvent(string username, string downloadName, float progress, string eventType = "updated")
    {
        return new SignalRMessage()
        {
            Name = DownloadProgress,
            Title = $"Downloading {downloadName}",
            SubTitle = $"Preparing {username.SentenceCase()} the download of {downloadName}",
            EventType = eventType,
            Progress = ProgressType.Determinate,
            Body = new
            {
                UserName = username,
                DownloadName = downloadName,
                Progress = progress
            }
        };
    }

    /// <summary>
    /// Represents a file being scanned by Kavita for processing and grouping
    /// </summary>
    /// <remarks>Does not have a progress as it's unknown how many files there are. Instead sends -1 to represent indeterminate</remarks>
    /// <param name="folderPath"></param>
    /// <param name="libraryName"></param>
    /// <param name="eventType"></param>
    /// <returns></returns>
    public static SignalRMessage FileScanProgressEvent(string folderPath, string libraryName, string eventType)
    {
        return new SignalRMessage()
        {
            Name = FileScanProgress,
            Title = $"Scanning {libraryName}",
            SubTitle = folderPath,
            EventType = eventType,
            Progress = ProgressType.Indeterminate,
            Body = new
            {
                Title = $"Scanning {libraryName}",
                Subtitle = folderPath,
                Filename = folderPath,
                EventTime = DateTime.Now,
            }
        };
    }

    /// <summary>
    /// This informs the UI with details about what is being processed by the Scanner
    /// </summary>
    /// <param name="libraryName"></param>
    /// <param name="eventType"></param>
    /// <param name="seriesName"></param>
    /// <returns></returns>
    public static SignalRMessage LibraryScanProgressEvent(string libraryName, string eventType, string seriesName = "")
    {
        return new SignalRMessage()
        {
            Name = ScanProgress,
            Title = $"Processing {seriesName}",
            SubTitle = seriesName,
            EventType = eventType,
            Progress = ProgressType.Indeterminate,
            Body = null
        };
    }

    public static SignalRMessage CoverUpdateEvent(int id, string entityType)
    {
        return new SignalRMessage()
        {
            Name = CoverUpdate,
            Title = "Updating Cover",
            Progress = ProgressType.None,
            Body = new
            {
                Id = id,
                EntityType = entityType,
            }
        };
    }

    public static SignalRMessage UserProgressUpdateEvent(int userId, string username, int seriesId, int volumeId, int chapterId, int pagesRead)
    {
        return new SignalRMessage()
        {
            Name = UserProgressUpdate,
            Title = "Updating User Progress",
            Progress = ProgressType.None,
            Body = new
            {
                UserId = userId,
                Username = username,
                SeriesId = seriesId,
                VolumeId = volumeId,
                ChapterId = chapterId,
                PagesRead = pagesRead,
            }
        };
    }

    public static SignalRMessage SiteThemeProgressEvent(string subtitle, string themeName, string eventType)
    {
        return new SignalRMessage()
        {
            Name = SiteThemeProgress,
            Title = "Scanning Site Theme",
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Indeterminate,
            Body = new
            {
                ThemeName = themeName,
            }
        };
    }

    public static SignalRMessage BookThemeProgressEvent(string subtitle, string themeName, string eventType)
    {
        return new SignalRMessage()
        {
            Name = BookThemeProgress,
            Title = "Scanning Book Theme",
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Indeterminate,
            Body = new
            {
                ThemeName = themeName,
            }
        };
    }

    public static SignalRMessage UserUpdateEvent(int userId, string userName)
    {
        return new SignalRMessage()
        {
            Name = UserUpdate,
            Title = "User Update",
            Progress = ProgressType.None,
            Body = new
            {
                UserId = userId,
                UserName = userName
            }
        };
    }

    public static SignalRMessage ConvertBookmarksProgressEvent(float progress, string eventType)
    {
        return new SignalRMessage()
        {
            Name = ConvertBookmarksProgress,
            Title = "Converting Bookmarks to WebP",
            SubTitle = string.Empty,
            EventType = eventType,
            Progress = ProgressType.Determinate,
            Body = new
            {
                Progress = progress,
                EventTime = DateTime.Now
            }
        };
    }

    public static SignalRMessage ConvertCoverProgressEvent(float progress, string eventType)
    {
        return new SignalRMessage()
        {
            Name = ConvertCoversProgress,
            Title = "Converting Covers to WebP",
            SubTitle = string.Empty,
            EventType = eventType,
            Progress = ProgressType.Determinate,
            Body = new
            {
                Progress = progress,
                EventTime = DateTime.Now
            }
        };
    }
}

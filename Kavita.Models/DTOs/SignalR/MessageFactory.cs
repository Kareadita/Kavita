using System;
using System.Globalization;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.Update;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;

namespace Kavita.Models.DTOs.SignalR;

public static class MessageFactoryEntityTypes
{
    public const string Library = "library";
    public const string Series = "series";
    public const string Volume = "volume";
    public const string Chapter = "chapter";
    public const string Collection = "collection";
    public const string ReadingList = "readingList";
    public const string Person = "person";
    public const string User = "user";
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
    /// When a Collection has been updated
    /// </summary>
    public const string CollectionUpdated = "CollectionUpdated";
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
    public const string DownloadProgress = "DownloadProgress";
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
    private const string ConvertCoversProgress = "ConvertCoversProgress";
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
    /// <summary>
    /// A Scrobbling Key has expired and needs rotation
    /// </summary>
    public const string ScrobblingKeyExpired = "ScrobblingKeyExpired";
    /// <summary>
    /// Order, Visibility, etc has changed on the Dashboard. UI will refresh the layout
    /// </summary>
    public const string DashboardUpdate = "DashboardUpdate";
    /// <summary>
    /// Order, Visibility, etc has changed on the Sidenav. UI will refresh the layout
    /// </summary>
    public const string SideNavUpdate = "SideNavUpdate";
    /// <summary>
    /// A Theme was updated and UI should refresh to get the latest version
    /// </summary>
    public const string SiteThemeUpdated = "SiteThemeUpdated";
    /// <summary>
    /// A Progress event when a smart collection is synchronizing
    /// </summary>
    public const string SmartCollectionSync = "SmartCollectionSync";
    /// <summary>
    /// Chapter is removed from server
    /// </summary>
    public const string ChapterRemoved = "ChapterRemoved";
    /// <summary>
    /// Chapter is updated
    /// </summary>
    public const string ChapterUpdated = "ChapterUpdated";
    /// <summary>
    /// Volume is removed from server
    /// </summary>
    public const string VolumeRemoved = "VolumeRemoved";
    /// <summary>
    /// A Person merged has been merged into another
    /// </summary>
    public const string PersonMerged = "PersonMerged";
    /// <summary>
    /// A Rate limit error was hit when matching a series with Kavita+
    /// </summary>
    public const string ExternalMatchRateLimitError = "ExternalMatchRateLimitError";
    /// <summary>
    /// Annotation is updated within the reader
    /// </summary>
    public const string AnnotationUpdate = "AnnotationUpdate";
    /// <summary>
    /// A Reading Session is starting or updating
    /// </summary>
    public const string ReadingSessionUpdate = "ReadingSessionUpdate";
    /// <summary>
    /// A Reading Session is closing
    /// </summary>
    public const string ReadingSessionClose = "ReadingSessionClose";
    /// <summary>
    /// Auth key has been rotated, created
    /// </summary>
    public const string AuthKeyUpdate = nameof(AuthKeyUpdate);
    /// <summary>
    /// An Auth key has been deleted
    /// </summary>
    public const string AuthKeyDeleted = nameof(AuthKeyDeleted);
    /// <summary>
    /// A reading list was updated via a Sync Operation
    /// </summary>
    public const string ReadingListUpdated = nameof(ReadingListUpdated);
    /// <summary>
    /// A series was updated (E.x. K+ match)
    /// </summary>
    public const string SeriesUpdated = nameof(SeriesUpdated);
    /// <summary>
    /// A scrobble provider has had their (authentication) details updated
    /// </summary>
    public const string ScrobbleProviderUpdated = nameof(ScrobbleProviderUpdated);
    /// <summary>
    /// The K+ license info has updated
    /// </summary>
    public const string LicenseInfoUpdate = nameof(LicenseInfoUpdate);
    /// <summary>
    /// The K+ Metadata for a series has been updated
    /// </summary>
    public const string ExternalMetadataUpdate = nameof(ExternalMetadataUpdate);
    /// <summary>
    /// Progress event send after a batch completes
    /// </summary>
    public const string RerunMetadataMappingsProgress = nameof(RerunMetadataMappingsProgress);


    public static SignalRMessageDto DashboardUpdateEvent(int userId)
    {
        return new SignalRMessageDto()
        {
            Name = DashboardUpdate,
            Priority = MessageEventPriority.Silent,
            Title = "Dashboard Update",
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                UserId = userId
            }
        };
    }

    public static SignalRMessageDto SideNavUpdateEvent(int userId)
    {
        return new SignalRMessageDto()
        {
            Name = SideNavUpdate,
            Priority = MessageEventPriority.Silent,
            Title = "SideNav Update",
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                UserId = userId
            }
        };
    }


    public static SignalRMessageDto ScanSeriesEvent(int libraryId, int seriesId, string seriesName)
    {
        return new SignalRMessageDto()
        {
            Name = ScanSeries,
            Priority = MessageEventPriority.Silent,
            EventType = ProgressEventType.Single,
            Body = new
            {
                LibraryId = libraryId,
                SeriesId = seriesId,
                SeriesName = seriesName
            }
        };
    }

    public static SignalRMessageDto SeriesAddedEvent(int seriesId, string seriesName, int libraryId)
    {
        return new SignalRMessageDto()
        {
            Name = SeriesAdded,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SeriesId = seriesId,
                SeriesName = seriesName,
                LibraryId = libraryId
            }
        };
    }

    public static SignalRMessageDto SeriesRemovedEvent(int seriesId, string seriesName, int libraryId)
    {
        return new SignalRMessageDto()
        {
            Name = SeriesRemoved,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SeriesId = seriesId,
                SeriesName = seriesName,
                LibraryId = libraryId
            }
        };
    }

    public static SignalRMessageDto ChapterRemovedEvent(int chapterId, int seriesId)
    {
        return new SignalRMessageDto()
        {
            Name = ChapterRemoved,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SeriesId = seriesId,
                ChapterId = chapterId
            }
        };
    }

    public static SignalRMessageDto ChapterUpdatedEvent(int chapterId, int seriesId)
    {
        return new SignalRMessageDto
        {
            Name = ChapterUpdated,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SeriesId = seriesId,
                ChapterId = chapterId
            }
        };
    }

    public static SignalRMessageDto VolumeRemovedEvent(int volumeId, int seriesId)
    {
        return new SignalRMessageDto()
        {
            Name = VolumeRemoved,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SeriesId = seriesId,
                VolumeId = volumeId
            }
        };
    }


    public static SignalRMessageDto WordCountAnalyzerProgressEvent(int libraryId, float progress, string eventType, string subtitle = "")
    {
        return new SignalRMessageDto()
        {
            Name = WordCountAnalyzerProgress,
            Priority = MessageEventPriority.Activity,
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

    public static SignalRMessageDto CoverUpdateProgressEvent(int libraryId, float progress, string eventType, string subtitle = "")
    {
        return new SignalRMessageDto()
        {
            Name = CoverUpdateProgress,
            Priority = MessageEventPriority.Activity,
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

    public static SignalRMessageDto BackupDatabaseProgressEvent(float progress, string subtitle = "")
    {
        return new SignalRMessageDto()
        {
            Name = BackupDatabaseProgress,
            Priority = MessageEventPriority.Activity,
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
    public static SignalRMessageDto CleanupProgressEvent(float progress, string subtitle = "")
    {
        return new SignalRMessageDto()
        {
            Name = CleanupProgress,
            Priority = MessageEventPriority.Activity,
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


    public static SignalRMessageDto UpdateVersionEvent(UpdateNotificationDto update)
    {
        return new SignalRMessageDto
        {
            Name = UpdateAvailable,
            Priority = MessageEventPriority.Action,
            Title = "Update Available",
            SubTitle = update.UpdateTitle,
            EventType = ProgressEventType.Single,
            Progress = ProgressType.None,
            Body = update
        };
    }

    public static SignalRMessageDto SendingToDeviceEvent(string subtitle, string eventType)
    {
        return new SignalRMessageDto
        {
            Name = SendingToDevice,
            Priority = MessageEventPriority.Activity,
            Title = "Sending files to Device",
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Indeterminate,
            Body = new { }
        };
    }


    public static SignalRMessageDto CollectionUpdatedEvent(int collectionId)
    {
        return new SignalRMessageDto
        {
            Name = CollectionUpdated,
            Priority = MessageEventPriority.Silent,
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                TagId = collectionId,
            }
        };
    }


    public static SignalRMessageDto ErrorEvent(string title, string subtitle)
    {
        return new SignalRMessageDto
        {
            Name = Error,
            Priority = MessageEventPriority.Error,
            Title = title,
            SubTitle = subtitle,
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                Name = Error,
                Title = title,
                SubTitle = subtitle,
            }
        };
    }

    public static SignalRMessageDto InfoEvent(string title, string subtitle)
    {
        return new SignalRMessageDto
        {
            Name = Info,
            Priority = MessageEventPriority.Info,
            Title = title,
            SubTitle = subtitle,
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                Name = Info,
                Title = title,
                SubTitle = subtitle,
            }
        };
    }

    #region Coded Error and Info

    // Body repeats Name, Title and SubTitle because the current widget reads them from the payload.
    // A LibraryId or SeriesId in Body narrows the audience of an onlyAdmins: false send to users with access
    private static SignalRMessageDto CodedEvent(string name, string code, string title, string subtitle, object body)
    {
        return new SignalRMessageDto
        {
            Name = name,
            Priority = name == Error ? MessageEventPriority.Error : MessageEventPriority.Info,
            Code = code,
            Title = title,
            SubTitle = subtitle,
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = body
        };
    }

    public static SignalRMessageDto RootFoldersInaccessibleEvent(int libraryId, string libraryName, string[] folders)
    {
        const string title = "Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted";
        var subtitle = string.Join(", ", folders);

        return CodedEvent(Error, MessageEventCode.RootFoldersInaccessible, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            LibraryName = libraryName,
            Folders = folders,
        });
    }

    public static SignalRMessageDto RootFoldersEmptyEvent(int libraryId, string libraryName)
    {
        var title = $"Some of the root folders for the library, {libraryName}, are empty.";
        const string subtitle = "Either your mount has been disconnected or you are trying to delete all series in the library. " +
                                "Scan has been aborted. " +
                                "Check that your mount is connected or change the library's root folder and rescan";

        return CodedEvent(Error, MessageEventCode.RootFoldersEmpty, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            LibraryName = libraryName,
        });
    }

    /// <param name="detailsHtml">Explanation and collision table, rendered as-is in the details dialog</param>
    public static SignalRMessageDto SeriesCollisionEvent(int libraryId, string libraryName, string seriesName, string detailsHtml)
    {
        var title = $"Series collision on \"{seriesName}\" in library {libraryName}";

        return CodedEvent(Error, MessageEventCode.SeriesCollision, title, detailsHtml, new
        {
            Name = Error,
            Title = title,
            SubTitle = detailsHtml,
            LibraryId = libraryId,
            LibraryName = libraryName,
            SeriesName = seriesName,
        });
    }

    /// <param name="seriesId">Null when the series was new and never saved</param>
    public static SignalRMessageDto FilesOutsideFolderEvent(int libraryId, int? seriesId, string seriesName)
    {
        var title = $"{seriesName} has files spread outside a single series folder";
        const string subtitle = "This has negative performance effects. Please ensure all series are under a single folder from library";

        return CodedEvent(Info, MessageEventCode.FilesOutsideFolder, title, subtitle, new
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
        });
    }

    public static SignalRMessageDto ScanSeriesNotNestedEvent(int libraryId, int seriesId, string seriesName)
    {
        var title = $"{seriesName} scan aborted";
        const string subtitle = "Files for series are not in a nested folder under library path. Correct this and rescan.";

        return CodedEvent(Error, MessageEventCode.ScanSeriesNotNested, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
        });
    }

    public static SignalRMessageDto ScanSeriesNoRootEvent(int libraryId, int seriesId, string seriesName)
    {
        var title = $"{seriesName} scan aborted";
        const string subtitle = "Scan Series could not find a single, valid folder root for files";

        return CodedEvent(Error, MessageEventCode.ScanSeriesNoRoot, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
        });
    }

    public static SignalRMessageDto ScanSeriesNoFilesEvent(int libraryId, int seriesId, string seriesName)
    {
        var title = $"Error scanning {seriesName}";
        const string subtitle = "We weren't able to find any files in the series scan, but there should be. Please correct your naming convention or put Series in a dedicated folder. Aborting scan";

        return CodedEvent(Error, MessageEventCode.ScanSeriesNoFiles, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
        });
    }

    public static SignalRMessageDto ScanSeriesFolderMissingEvent(int libraryId, int seriesId, string seriesName)
    {
        var title = $"{seriesName} folder is missing";
        const string subtitle = "The folder the series was in is missing. Delete series manually or perform a library scan.";

        return CodedEvent(Info, MessageEventCode.ScanSeriesFolderMissing, title, subtitle, new
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
        });
    }

    public static SignalRMessageDto ScanNoWorkEvent(int libraryId, int seriesId, string seriesName, DateTime lastFolderScanned)
    {
        var title = $"{seriesName} scan has no work to do";
        var subtitle = $"All folders have not been changed since last scan ({lastFolderScanned.ToString(CultureInfo.CurrentCulture)}). Scan will be aborted.";

        return CodedEvent(Info, MessageEventCode.ScanNoWork, title, subtitle, new
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
        });
    }

    /// <param name="seriesId">Null when the series was new and never saved</param>
    public static SignalRMessageDto DbWriteFailedEvent(int libraryId, int? seriesId, string seriesName, string error)
    {
        var title = $"There was an issue writing to the DB for Series {seriesName}";

        return CodedEvent(Error, MessageEventCode.DbWriteFailed, title, error, new
        {
            Name = Error,
            Title = title,
            SubTitle = error,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
        });
    }

    /// <param name="scheduledForUtc">Pass the same value given to Hangfire, the UI matches it to the scheduled job</param>
    public static SignalRMessageDto ScanLibrariesDelayedEvent(DateTime scheduledForUtc)
    {
        const string title = "Scan libraries task delayed";
        var subtitle = $"A scan was ongoing during processing of the scan libraries task. Task has been rescheduled for 3 hours: {scheduledForUtc.ToLocalTime()}";

        return CodedEvent(Info, MessageEventCode.ScanLibrariesDelayed, title, subtitle, new
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
            ScheduledForUtc = scheduledForUtc,
        });
    }

    /// <inheritdoc cref="ScanLibrariesDelayedEvent"/>
    public static SignalRMessageDto ScanLibraryDelayedEvent(int libraryId, string libraryName, DateTime scheduledForUtc)
    {
        const string title = "Scan library task delayed";
        var subtitle = $"A scan was ongoing during processing of the {libraryName} scan task. Task has been rescheduled for 3 hours: {scheduledForUtc.ToLocalTime()}";

        return CodedEvent(Info, MessageEventCode.ScanLibraryDelayed, title, subtitle, new
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            LibraryName = libraryName,
            ScheduledForUtc = scheduledForUtc,
        });
    }

    /// <inheritdoc cref="ScanLibrariesDelayedEvent"/>
    public static SignalRMessageDto ScanSeriesDelayedEvent(int libraryId, int seriesId, string seriesName, DateTime scheduledForUtc)
    {
        var title = $"Scan series task delayed: {seriesName}";
        var subtitle = $"A scan was ongoing during processing of the scan series task. Task has been rescheduled for 10 minutes: {scheduledForUtc.ToLocalTime()}";

        return CodedEvent(Info, MessageEventCode.ScanSeriesDelayed, title, subtitle, new
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
            ScheduledForUtc = scheduledForUtc,
        });
    }

    public static SignalRMessageDto BackupFolderUnwritableEvent(string folder)
    {
        const string title = "Backup Service Error";
        var subtitle = $"Could not write to {folder}; aborting backup";

        return CodedEvent(Error, MessageEventCode.BackupFolderUnwritable, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            Folder = folder,
        });
    }

    public static SignalRMessageDto BackupExistsEvent(string path)
    {
        const string title = "Backup Service Error";
        var subtitle = $"{path} already exists, aborting";

        return CodedEvent(Error, MessageEventCode.BackupExists, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            Path = path,
        });
    }

    public static SignalRMessageDto CleanupOnHoldEvent()
    {
        const string title = "Cleanup";
        const string subtitle = "Cleanup put on hold as a media conversion in progress";

        return CodedEvent(Info, MessageEventCode.CleanupOnHold, title, subtitle, new
        {
            Name = Info,
            Title = title,
            SubTitle = subtitle,
        });
    }

    public static SignalRMessageDto WordCountFailedEvent(int libraryId, int seriesId, string seriesName, string filePath)
    {
        const string title = "There was an issue counting words on an epub";
        var subtitle = $"{seriesName} - {filePath}";

        return CodedEvent(Error, MessageEventCode.WordCountFailed, title, subtitle, new
        {
            Name = Error,
            Title = title,
            SubTitle = subtitle,
            LibraryId = libraryId,
            SeriesId = seriesId,
            SeriesName = seriesName,
            FilePath = filePath,
        });
    }

    #endregion

    public static SignalRMessageDto LibraryModifiedEvent(int libraryId, string action)
    {
        return new SignalRMessageDto
        {
            Name = LibraryModified,
            Priority = MessageEventPriority.Silent,
            Title = "Library modified",
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                LibraryId = libraryId,
                Action = action,
            }
        };
    }

    public static SignalRMessageDto DownloadProgressEvent(string username, string downloadName, string subtitle, float progress, string eventType = "updated", string? correlationId = null)
    {
        return new SignalRMessageDto()
        {
            Name = DownloadProgress,
            Priority = MessageEventPriority.Activity,
            CorrelationId = correlationId,
            Title = $"Preparing {username.SentenceCase()} the download of {downloadName}",
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Determinate,
            Body = new
            {
                UserName = username,
                DownloadName = downloadName,
                Progress = progress,
                CorrelationId = correlationId
            }
        };
    }

    /// <summary>
    /// Represents a file being scanned by Kavita for processing and grouping
    /// </summary>
    /// <remarks>Determinate only when <paramref name="current"/> and <paramref name="total"/> are known</remarks>
    /// <param name="folderPath"></param>
    /// <param name="libraryId"></param>
    /// <param name="libraryName"></param>
    /// <param name="eventType"></param>
    /// <param name="code">Which scan step this belongs to, see <see cref="MessageEventCode"/></param>
    /// <param name="current">1-based position within the step</param>
    /// <param name="total">Items in the step</param>
    /// <returns></returns>
    public static SignalRMessageDto FileScanProgressEvent(string folderPath, int libraryId, string libraryName, string eventType,
        string? code = null, int? current = null, int? total = null)
    {
        var hasProgress = current.HasValue && total is > 0;

        return new SignalRMessageDto()
        {
            Name = FileScanProgress,
            Priority = MessageEventPriority.Activity,
            Code = code,
            Title = $"Scanning {libraryName}",
            SubTitle = folderPath,
            EventType = eventType,
            Progress = hasProgress ? ProgressType.Determinate : ProgressType.Indeterminate,
            Body = new
            {
                Title = $"Scanning {libraryName}",
                Subtitle = folderPath,
                Filename = folderPath,
                LibraryId = libraryId,
                LibraryName = libraryName,
                EventTime = DateTime.Now,
                Current = current,
                Total = total,
                Progress = hasProgress ? Math.Clamp(current!.Value / (float) total!.Value, 0f, 1f) : (float?) null,
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
    public static SignalRMessageDto SmartCollectionProgressEvent(string collectionName, string seriesName, int currentItems, int totalItems, string eventType)
    {
        return new SignalRMessageDto()
        {
            Name = SmartCollectionSync,
            Priority = MessageEventPriority.Activity,
            Title = $"Synchronizing {collectionName}",
            SubTitle = seriesName,
            EventType = eventType,
            Progress = ProgressType.Determinate,
            Body = new
            {
                CollectionName = collectionName,
                Progress = totalItems <= 0 ? 0f : Math.Clamp(currentItems / (float) totalItems, 0f, 1f),
                EventTime = DateTime.Now
            }
        };
    }

    /// <summary>
    /// This informs the UI with details about what is being processed by the Scanner
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="libraryName"></param>
    /// <param name="eventType"></param>
    /// <param name="seriesName"></param>
    /// <param name="leftToProcess"></param>
    /// <param name="totalToProcess"></param>
    /// <returns></returns>
    public static SignalRMessageDto LibraryScanProgressEvent(int libraryId, string libraryName, string eventType, string seriesName = "", int? leftToProcess = null, int? totalToProcess = null)
    {
        var hasProgress = totalToProcess.HasValue && leftToProcess.HasValue;

        return new SignalRMessageDto()
        {
            Name = ScanProgress,
            Priority = MessageEventPriority.Activity,
            Code = MessageEventCode.ScanProcessingSeries,
            Title = $"Processing {seriesName}",
            SubTitle = seriesName,
            EventType = eventType,
            Progress = hasProgress ?  ProgressType.Determinate : ProgressType.Indeterminate,
            Body = new
            {
                SeriesName = seriesName,
                LibraryId = libraryId,
                LibraryName = libraryName,
                LeftToProcess = leftToProcess,
                TotalToProcess = totalToProcess,
                Progress = hasProgress ? (totalToProcess - leftToProcess) / (float) totalToProcess.Value : null,
            }
        };
    }

    public static SignalRMessageDto CoverUpdateEvent(int id, string entityType)
    {
        return new SignalRMessageDto()
        {
            Name = CoverUpdate,
            Priority = MessageEventPriority.Silent,
            Title = "Updating Cover",
            Progress = ProgressType.None,
            Body = new
            {
                Id = id,
                EntityType = entityType,
            }
        };
    }

    public static SignalRMessageDto UserProgressUpdateEvent(int userId, int seriesId, int volumeId, int chapterId, int pagesRead)
    {
        return new SignalRMessageDto()
        {
            Name = UserProgressUpdate,
            Priority = MessageEventPriority.Silent,
            Title = "Updating User Progress",
            Progress = ProgressType.None,
            Body = new
            {
                UserId = userId,
                SeriesId = seriesId,
                VolumeId = volumeId,
                ChapterId = chapterId,
                PagesRead = pagesRead,
            }
        };
    }

    public static SignalRMessageDto SiteThemeProgressEvent(string subtitle, string themeName, string eventType)
    {
        return new SignalRMessageDto()
        {
            Name = SiteThemeProgress,
            Priority = MessageEventPriority.Activity,
            Title = "Processing Site Theme", // TODO: Localize SignalRMessage titles
            SubTitle = subtitle,
            EventType = eventType,
            Progress = ProgressType.Indeterminate,
            Body = new
            {
                ThemeName = themeName,
            }
        };
    }

    /// <summary>
    /// Sends an event to the UI informing of a SiteTheme update and UI needs to refresh the content
    /// </summary>
    /// <param name="themeName"></param>
    /// <returns></returns>
    public static SignalRMessageDto SiteThemeUpdatedEvent(string themeName)
    {
        return new SignalRMessageDto()
        {
            Name = SiteThemeUpdated,
            Priority = MessageEventPriority.Silent,
            Title = "SiteTheme Update",
            Progress = ProgressType.None,
            Body = new
            {
                ThemeName = themeName,
            }
        };
    }

    public static SignalRMessageDto BookThemeProgressEvent(string subtitle, string themeName, string eventType)
    {
        return new SignalRMessageDto()
        {
            Name = BookThemeProgress,
            Priority = MessageEventPriority.Activity,
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

    public static SignalRMessageDto UserUpdateEvent(int userId, string userName)
    {
        return new SignalRMessageDto()
        {
            Name = UserUpdate,
            Priority = MessageEventPriority.Silent,
            Title = "User Update",
            Progress = ProgressType.None,
            Body = new
            {
                UserId = userId,
                UserName = userName
            }
        };
    }

    public static SignalRMessageDto ConvertBookmarksProgressEvent(float progress, string eventType)
    {
        return new SignalRMessageDto()
        {
            Name = ConvertBookmarksProgress,
            Priority = MessageEventPriority.Activity,
            Title = "Converting Bookmarks",
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

    public static SignalRMessageDto ConvertCoverProgressEvent(float progress, string eventType)
    {
        return new SignalRMessageDto()
        {
            Name = ConvertCoversProgress,
            Priority = MessageEventPriority.Activity,
            Title = "Converting Covers",
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

    public static SignalRMessageDto ScrobblingKeyExpiredEvent(ScrobbleProvider provider)
    {
        return new SignalRMessageDto
        {
            Name = ScrobblingKeyExpired,
            Priority = MessageEventPriority.Action,
            Title = "Scrobbling Key Expired",
            SubTitle = provider + " expired. Please re-generate on User Account page.",
            Progress = ProgressType.None,
            EventType = ProgressEventType.Single,
            Body = new
            {
                Provider = provider
            }
        };
    }

    public static SignalRMessageDto PersonMergedMessage(Entities.Person.Person dst, Entities.Person.Person src)
    {
        return new SignalRMessageDto()
        {
            Name = PersonMerged,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                srcId = src.Id,
                dstName = dst.Name,
            },
        };
    }

    public static SignalRMessageDto ExternalMatchRateLimitErrorEvent(int seriesId, string seriesName)
    {
        return new SignalRMessageDto()
        {
            Name = ExternalMatchRateLimitError,
            Priority = MessageEventPriority.Error,
            Body = new
            {
                seriesId,
                seriesName,
            },
        };
    }

    public static SignalRMessageDto AnnotationUpdateEvent(AnnotationDto dto)
    {
        return new SignalRMessageDto()
        {
            Name = AnnotationUpdate,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                Annotation = dto
            },
        };
    }

    public static SignalRMessageDto ReadingSessionUpdateEvent(int userId, int sessionId)
    {
        return new SignalRMessageDto()
        {
            Name = ReadingSessionUpdate,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SessionId = sessionId,
                UserId = userId,
            }
        };
    }

    public static SignalRMessageDto ReadingSessionCloseEvent(int userId, int sessionId)
    {
        return new SignalRMessageDto()
        {
            Name = ReadingSessionClose,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SessionId = sessionId,
                UserId = userId,
            }
        };
    }

    public static SignalRMessageDto AuthKeyUpdatedEvent(AuthKeyDto authKey)
    {
        return new SignalRMessageDto
        {
            Name = AuthKeyUpdate,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                AuthKey = authKey
            }
        };
    }

    public static SignalRMessageDto AuthKeyDeletedEvent(int id)
    {
        return new SignalRMessageDto
        {
            Name = AuthKeyDeleted,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                Id = id
            }
        };
    }

    public static SignalRMessageDto ReadingListUpdatedEvent(int id)
    {
        return new SignalRMessageDto
        {
            Name = ReadingListUpdated,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                Id = id
            }
        };
    }

    public static SignalRMessageDto SeriesUpdatedEvent(int seriesId)
    {
        return new SignalRMessageDto
        {
            Name = SeriesUpdated,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                Id = seriesId
            }
        };
    }

    public static SignalRMessageDto ScrobbleProviderUpdatedEvent(ScrobbleProvider provider)
    {
        return new SignalRMessageDto
        {
            Name = ScrobbleProviderUpdated,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                Provider = provider
            }
        };
    }

    public static SignalRMessageDto LicenseInfoUpdateEvent()
    {
        return new SignalRMessageDto
        {
            Name = LicenseInfoUpdate,
            Priority = MessageEventPriority.Silent,
        };
    }

    public static SignalRMessageDto ExternalMetadataUpdateEvent(int seriesId)
    {
        return new SignalRMessageDto
        {
            Name = ExternalMetadataUpdate,
            Priority = MessageEventPriority.Silent,
            Body = new
            {
                SeriesId = seriesId
            }
        };
    }

    public static SignalRMessageDto ReRunMappingsProgressEvent(string progressEventType, float progress)
    {
        return new SignalRMessageDto()
        {
            Name = RerunMetadataMappingsProgress,
            Priority = MessageEventPriority.Activity,
            Title = "Rerun Metadata Mappings",
            Progress = ProgressType.Determinate,
            EventType = progressEventType,
            Body = new
            {
                Progress = progress,
            }
        };
    }
}

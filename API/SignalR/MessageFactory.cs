using System;
using System.Diagnostics;
using System.Threading;
using API.DTOs.Update;
using API.Entities;

namespace API.SignalR
{
    public static class MessageFactory
    {
        /// <summary>
        /// An update is available for the Kavita instance
        /// </summary>
        public const string UpdateAvailable = "UpdateAvailable";
        /// <summary>
        /// Used to tell when a scan series completes
        /// </summary>
        public const string ScanSeries = "ScanSeries";
        /// <summary>
        /// Event sent out during Refresh Metadata for progress tracking
        /// </summary>
        private const string RefreshMetadataProgress = "RefreshMetadataProgress";
        /// <summary>
        /// Series is added to server
        /// </summary>
        public const string SeriesAdded = "SeriesAdded";
        /// <summary>
        /// Series is removed from server
        /// </summary>
        public const string SeriesRemoved = "SeriesRemoved";
        /// <summary>
        /// Progress event for Scan library. Deprecated in favor of ScanProgress
        /// </summary>
        //public const string ScanLibraryProgress = "ScanLibraryProgress";
        /// <summary>
        /// When a user is connects/disconnects from server
        /// </summary>
        public const string OnlineUsers = "OnlineUsers";
        /// <summary>
        /// When a series is added to a collection
        /// </summary>
        public const string SeriesAddedToCollection = "SeriesAddedToCollection";
        /// <summary>
        /// When an error occurs during a scan library task
        /// </summary>
        public const string ScanLibraryError = "ScanLibraryError";
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
        /// A type of event that has progress (determinate or indeterminate).
        /// The underlying event will have a name to give details on how to handle.
        /// </summary>
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


        public static SignalRMessage ScanSeriesEvent(int seriesId, string seriesName)
        {
            return new SignalRMessage()
            {
                Name = ScanSeries,
                Body = new
                {
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

        // public static SignalRMessage ScanLibraryProgressEvent(int libraryId, float progress)
        // {
        //     // How does this differ from DBupdateEvent?
        //     return new SignalRMessage()
        //     {
        //         Name = ScanLibraryProgress,
        //         Title = "Library Scan", // TODO: Use Library Name here
        //         SubTitle = "",
        //         EventType = progress switch
        //         {
        //             0f => "started",
        //             1f => "ended",
        //             _ => "updated"
        //         },
        //         Body = new
        //         {
        //             LibraryId = libraryId,
        //             Progress = progress,
        //             EventTime = DateTime.Now
        //         }
        //     };
        // }

        public static SignalRMessage RefreshMetadataProgressEvent(int libraryId, float progress, string subtitle = "")
        {
            return new SignalRMessage()
            {
                Name = RefreshMetadataProgress,
                Title = "Refreshing Covers",
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

        public static SignalRMessage ScanLibraryErrorEvent(int libraryId, string libraryName)
        {
            return new SignalRMessage
            {
                Name = ScanLibraryError,
                Title = "Error",
                SubTitle = $"Error Scanning {libraryName}",
                Progress = ProgressType.None,
                EventType = ProgressEventType.Single,
                Body = new
                {
                    LibraryId = libraryId,
                }
            };
        }

        public static SignalRMessage DownloadProgressEvent(string username, string downloadName, float progress, string eventType = "updated")
        {
            return new SignalRMessage()
            {
                Name = DownloadProgress,
                Title = $"Downloading {downloadName}",
                SubTitle = $"{username} is downloading {downloadName}",
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
        /// <param name="filename"></param>
        /// <param name="libraryName"></param>
        /// <param name="eventType"></param>
        /// <returns></returns>
        public static SignalRMessage FileScanProgressEvent(string filename, string libraryName, string eventType)
        {
            return new SignalRMessage()
            {
                Name = FileScanProgress,
                Title = $"Scanning {libraryName}",
                SubTitle = filename,
                EventType = eventType,
                Progress = ProgressType.Indeterminate,
                Body = new
                {
                    Title = $"Scanning {libraryName}",
                    Subtitle = filename,
                    EventTime = DateTime.Now,
                }
            };
        }

        public static SignalRMessage DbUpdateProgressEvent(Series series, string eventType)
        {
            // TODO: I want this as a detail of a Scanning Series and we can put more information like Volume or Chapter here
            return new SignalRMessage()
            {
                Name = ScanProgress,
                Title = $"Scanning {series.Library.Name}",
                SubTitle = series.Name,
                EventType = eventType,
                Progress = ProgressType.Indeterminate,
                Body = new
                {
                    Title = "Updating Series",
                    SubTitle = series.Name
                }
            };
        }

        public static SignalRMessage CoverUpdateEvent(int id, string entityType)
        {
            return new SignalRMessage()
            {
                Name = CoverUpdate,
                Title = "Updating Cover",
                //SubTitle = series.Name, // TODO: Refactor this
                Progress = ProgressType.None,
                Body = new
                {
                    Id = id,
                    EntityType = entityType,
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
    }
}

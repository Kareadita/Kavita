using System;
using System.Diagnostics;
using System.Threading;
using API.DTOs.Update;
using API.Entities;

namespace API.SignalR
{
    public static class MessageFactory
    {
        public static SignalRMessage ScanSeriesEvent(int seriesId, string seriesName)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.ScanSeries,
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
                Name = SignalREvents.SeriesAdded,
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
                Name = SignalREvents.SeriesRemoved,
                Body = new
                {
                    SeriesId = seriesId,
                    SeriesName = seriesName,
                    LibraryId = libraryId
                }
            };
        }

        public static SignalRMessage ScanLibraryProgressEvent(int libraryId, float progress)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.ScanLibraryProgress,
                EventType = progress switch
                {
                    0f => "started",
                    1f => "ended",
                    _ => "updated"
                },
                Body = new
                {
                    LibraryId = libraryId,
                    Progress = progress,
                    EventTime = DateTime.Now
                }
            };
        }

        public static SignalRMessage RefreshMetadataProgressEvent(int libraryId, float progress, string subtitle = "")
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.RefreshMetadataProgress,
                Title = "Refreshing Covers",
                SubTitle = subtitle,
                EventType = progress switch
                {
                    0f => "started",
                    1f => "ended",
                    _ => "updated"
                },
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
                Name = SignalREvents.BackupDatabaseProgress,
                Title = "Backing up Database",
                SubTitle = subtitle,
                EventType = progress switch
                {
                    0f => "started",
                    1f => "ended",
                    _ => "updated"
                },
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
                Name = SignalREvents.CleanupProgress,
                Title = "Performing Cleanup",
                SubTitle = subtitle,
                EventType = progress switch
                {
                    0f => "started",
                    1f => "ended",
                    _ => "updated"
                },
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
                Name = SignalREvents.UpdateAvailable,
                Title = "Update Available",
                SubTitle = update.UpdateTitle,
                EventType = "single",
                Body = update
            };
        }

        public static SignalRMessage SeriesAddedToCollection(int tagId, int seriesId)
        {
            return new SignalRMessage
            {
                Name = SignalREvents.SeriesAddedToCollection,
                Body = new
                {
                    TagId = tagId,
                    SeriesId = seriesId
                }
            };
        }

        public static SignalRMessage ScanLibraryError(int libraryId, string libraryName)
        {
            return new SignalRMessage
            {
                Name = SignalREvents.ScanLibraryError,
                Title = "Error",
                SubTitle = $"Error Scanning {libraryName}",
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
                Name = SignalREvents.DownloadProgress,
                Title = $"Downloading {downloadName}",
                SubTitle = $"{username} is downloading {downloadName}",
                EventType = eventType,
                Body = new
                {
                    UserName = username,
                    DownloadName = downloadName,
                    Progress = progress
                }
            };
        }


        public static SignalRMessage FileScanProgressEvent(string filename, string libraryName, string eventType)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.FileScanProgress,
                Title = $"Scanning {libraryName}",
                SubTitle = filename,
                EventType = eventType,
                Body = new
                {
                    Title = $"Scanning {libraryName}",
                    Subtitle = filename,
                    EventTime = DateTime.Now
                }
            };
        }

        public static SignalRMessage DbUpdateProgressEvent(Series series, string eventType)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.ScanProgress,
                Title = "Updating Series",
                SubTitle = series.Name,
                EventType = eventType,
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
                Name = SignalREvents.CoverUpdate,
                Title = "Updating Cover",
                //SubTitle = series.Name, // TODO: Refactor this
                Body = new
                {
                    Id = id,
                    EntityType = entityType,
                }
            };
        }

        public static SignalRMessage SiteThemeProgressEvent(string subtitle, int themeIteratedCount, int totalThemesToIterate, string themeName, float progress)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.SiteThemeProgress,
                Title = "Scanning Site Theme",
                SubTitle = subtitle,
                Body = new
                {
                    TotalUpdates = totalThemesToIterate,
                    CurrentCount = themeIteratedCount,
                    ThemeName = themeName,
                    Progress = progress
                }
            };
        }
    }
}

using System;
using API.DTOs.Update;

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
                Name = SignalREvents.ScanLibrary,
                Body = new
                {
                    LibraryId = libraryId,
                    Progress = progress,
                    EventTime = DateTime.Now
                }
            };
        }

        public static SignalRMessage RefreshMetadataProgressEvent(int libraryId, float progress)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.RefreshMetadataProgress,
                Body = new
                {
                    LibraryId = libraryId,
                    Progress = progress,
                    EventTime = DateTime.Now
                }
            };
        }



        public static SignalRMessage RefreshMetadataEvent(int libraryId, int seriesId)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.RefreshMetadata,
                Body = new
                {
                    SeriesId = seriesId,
                    LibraryId = libraryId
                }
            };
        }

        public static SignalRMessage BackupDatabaseProgressEvent(float progress)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.BackupDatabaseProgress,
                Body = new
                {
                    Progress = progress
                }
            };
        }
        public static SignalRMessage CleanupProgressEvent(float progress)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.CleanupProgress,
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
                Name = SignalREvents.UpdateVersion,
                Body = update
            };
        }

        public static SignalRMessage SeriesAddedToCollection(int tagId, int seriesId)
        {
            return new SignalRMessage
            {
                Name = SignalREvents.UpdateVersion,
                Body = new
                {
                    TagId = tagId,
                    SeriesId = seriesId
                }
            };
        }

        public static SignalRMessage ScanLibraryError(int libraryId)
        {
            return new SignalRMessage
            {
                Name = SignalREvents.ScanLibraryError,
                Body = new
                {
                    LibraryId = libraryId,
                }
            };
        }
    }
}

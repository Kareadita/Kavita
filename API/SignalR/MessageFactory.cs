using System.Threading;
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

        public static SignalRMessage ScanLibraryEvent(int libraryId, string stage)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.ScanLibrary,
                Body = new
                {
                    LibraryId = libraryId,
                    Stage = stage
                }
            };
        }

        public static SignalRMessage ScanLibraryProgressEvent(int libraryId, int progress, string seriesName)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.ScanLibrary,
                Body = new
                {
                    LibraryId = libraryId,
                    Progress = progress,
                    SeriesName = seriesName
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

        public static SignalRMessage UpdateVersionEvent(UpdateNotificationDto update)
        {
            return new SignalRMessage
            {
                Name = SignalREvents.UpdateVersion,
                Body = update
            };
        }

    }
}

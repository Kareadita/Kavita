using System.Threading;
using API.DTOs.Update;

namespace API.SignalR
{
    public static class MessageFactory
    {
        public static SignalRMessage ScanSeriesEvent(int seriesId)
        {
            return new SignalRMessage()
            {
                Name = SignalREvents.ScanSeries,
                Body = new
                {
                    SeriesId = seriesId
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

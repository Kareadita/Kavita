namespace API.Interfaces
{
    public interface ITaskScheduler
    {
        void ScanLibrary(int libraryId, bool forceUpdate = false);
        void CleanupVolumes(int[] volumeIds);
        void ScanSeries(int libraryId, int seriesId);
    }
}
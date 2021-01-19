namespace API.Interfaces
{
    public interface ITaskScheduler
    {
        public void ScanLibrary(int libraryId, bool forceUpdate = false);

        public void CleanupVolumes(int[] volumeIds);
    }
}
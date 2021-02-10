namespace API.Interfaces
{
    public interface ITaskScheduler
    {
        void ScanLibrary(int libraryId, bool forceUpdate = false);
        void CleanupChapters(int[] chapterIds);
        void RefreshMetadata(int libraryId, bool forceUpdate = true);
    }
}
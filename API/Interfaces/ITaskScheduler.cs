namespace API.Interfaces
{
    public interface ITaskScheduler
    {
        void ScanLibrary(int libraryId, bool forceUpdate = false);
        void CleanupChapters(int[] chapterIds);
    }
}
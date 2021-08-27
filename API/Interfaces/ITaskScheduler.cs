namespace API.Interfaces
{
    public interface ITaskScheduler
    {
        /// <summary>
        /// For use on Server startup
        /// </summary>
        void ScheduleTasks();
        void ScheduleStatsTasks();
        void ScheduleUpdaterTasks();
        void ScanLibrary(int libraryId, bool forceUpdate = false);
        void CleanupChapters(int[] chapterIds);
        void RefreshMetadata(int libraryId, bool forceUpdate = true);
        void CleanupTemp();
        void RefreshSeriesMetadata(int libraryId, int seriesId);
        void ScanSeries(int libraryId, int seriesId, bool forceUpdate = false);
        void CancelStatsTasks();
    }
}

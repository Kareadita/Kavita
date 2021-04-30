using API.Entities;

namespace API.Interfaces.Services
{
    public interface IMetadataService
    {
        /// <summary>
        /// Recalculates metadata for all entities in a library.
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="forceUpdate"></param>
        void RefreshMetadata(int libraryId, bool forceUpdate = false);

        public void UpdateMetadata(Chapter chapter, bool forceUpdate);
        public void UpdateMetadata(Volume volume, bool forceUpdate);
        public void UpdateMetadata(Series series, bool forceUpdate);
        /// <summary>
        /// Performs a forced refresh of metatdata just for a series and it's nested entities
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="seriesId"></param>
        void RefreshMetadataForSeries(int libraryId, int seriesId);
    }
}
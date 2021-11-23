using System.Threading.Tasks;
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
        Task RefreshMetadata(int libraryId, bool forceUpdate = false);
        /// <summary>
        /// Performs a forced refresh of metatdata just for a series and it's nested entities
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="seriesId"></param>
        Task RefreshMetadataForSeries(int libraryId, int seriesId, bool forceUpdate = false);
    }
}

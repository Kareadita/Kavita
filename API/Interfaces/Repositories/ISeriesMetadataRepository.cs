using API.Entities;
using API.Entities.Metadata;

namespace API.Interfaces.Repositories
{
    public interface ISeriesMetadataRepository
    {
        void Update(SeriesMetadata seriesMetadata);
    }
}

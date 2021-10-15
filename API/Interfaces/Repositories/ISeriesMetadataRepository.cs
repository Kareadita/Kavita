using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface ISeriesMetadataRepository
    {
        void Update(SeriesMetadata seriesMetadata);
    }
}

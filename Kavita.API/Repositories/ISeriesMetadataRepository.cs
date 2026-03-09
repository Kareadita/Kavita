using Kavita.Models.Entities.Metadata;

namespace Kavita.API.Repositories;

public interface ISeriesMetadataRepository
{
    void Update(SeriesMetadata seriesMetadata);
}

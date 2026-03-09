using Kavita.API.Repositories;
using Kavita.Models.Entities.Metadata;

namespace Kavita.Database.Repositories;



public class SeriesMetadataRepository(DataContext context) : ISeriesMetadataRepository
{
    public void Update(SeriesMetadata seriesMetadata)
    {
        context.SeriesMetadata.Update(seriesMetadata);
    }
}

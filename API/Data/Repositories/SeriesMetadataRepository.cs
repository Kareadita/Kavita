using API.Entities.Metadata;

namespace API.Data.Repositories;

public interface ISeriesMetadataRepository
{
    void Update(SeriesMetadata seriesMetadata);
}

public class SeriesMetadataRepository : ISeriesMetadataRepository
{
    private readonly DataContext _context;

    public SeriesMetadataRepository(DataContext context)
    {
        _context = context;
    }

    public void Update(SeriesMetadata seriesMetadata)
    {
        _context.SeriesMetadata.Update(seriesMetadata);
    }
}

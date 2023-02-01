using API.Entities.Metadata;

namespace API.Data.Repositories;

public interface ISeriesMetadataRepository
{
    void Update(SeriesMetadata seriesMetadata);
    void Attach(SeriesMetadata seriesMetadata);
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

    public void Attach(SeriesMetadata seriesMetadata)
    {
        _context.SeriesMetadata.Attach(seriesMetadata);
    }
}

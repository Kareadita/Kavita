using API.Entities;
using API.Interfaces.Repositories;

namespace API.Data.Repositories
{
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
}

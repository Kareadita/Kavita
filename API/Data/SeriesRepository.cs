using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class SeriesRepository : ISeriesRepository
    {
        private readonly DataContext _context;

        public SeriesRepository(DataContext context)
        {
            _context = context;
        }

        public void Update(Series series)
        {
            _context.Entry(series).State = EntityState.Modified;
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
        
        public bool SaveAll()
        {
            return _context.SaveChanges() > 0;
        }

        public async Task<Series> GetSeriesByNameAsync(string name)
        {
            return await _context.Series.SingleOrDefaultAsync(x => x.Name == name);
        }
        
        public Series GetSeriesByName(string name)
        {
            return _context.Series.SingleOrDefault(x => x.Name == name);
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class ChapterRepository : IChapterRepository
    {
        private readonly DataContext _context;

        public ChapterRepository(DataContext context)
        {
            _context = context;
        }

        public void Update(Chapter chapter)
        {
            _context.Entry(chapter).State = EntityState.Modified;
        }

        public async Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds)
        {
            return await _context.Chapter.Where(c => chapterIds.Contains(c.Id)).ToListAsync();
        }

        // TODO: Move over Chapter based queries here
    }
}

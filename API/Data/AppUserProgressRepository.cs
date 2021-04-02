using System.Linq;
using System.Threading.Tasks;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class AppUserProgressRepository : IAppUserProgressRepository
    {
        private readonly DataContext _context;

        public AppUserProgressRepository(DataContext context)
        {
            _context = context;
        }

        /// <summary>
        /// This will remove any entries that have chapterIds that no longer exists. This will execute the save as well.
        /// </summary>
        public async Task<bool> CleanupAbandonedChapters()
        {
            //SELECT * from AppUserProgresses as AUP WHERE ChapterId NOT IN (SELECT Id From Chapter);
            var chapterIds = _context.Chapter.Select(c => c.Id);

            var rowsToRemove = await _context.AppUserProgresses
                .Where(progress => !chapterIds.Contains(progress.ChapterId))
                .ToListAsync();
            
            _context.RemoveRange(rowsToRemove);
            return (await _context.SaveChangesAsync()) > 0;
        }
    }
}
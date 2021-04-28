using System.Linq;
using System.Threading.Tasks;
using API.Entities.Enums;
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
        public async Task<int> CleanupAbandonedChapters()
        {
            var chapterIds = _context.Chapter.Select(c => c.Id);

            var rowsToRemove = await _context.AppUserProgresses
                .Where(progress => !chapterIds.Contains(progress.ChapterId))
                .ToListAsync();
            
            _context.RemoveRange(rowsToRemove);
            return await _context.SaveChangesAsync() > 0 ? rowsToRemove.Count : 0;
        }

        /// <summary>
        /// Checks if user has any progress against a library of passed type
        /// </summary>
        /// <param name="libraryType"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<bool> UserHasProgress(LibraryType libraryType, int userId)
        {
            var seriesIds = await _context.AppUserProgresses
                .Where(aup => aup.PagesRead > 0 && aup.AppUserId == userId)
                .AsNoTracking()
                .Select(aup => aup.SeriesId)
                .ToListAsync();

            if (seriesIds.Count == 0) return false;
            
            return await _context.Series
                .Include(s => s.Library)
                .Where(s => seriesIds.Contains(s.Id) && s.Library.Type == libraryType)
                .AsNoTracking()
                .AnyAsync();
        }
    }
}
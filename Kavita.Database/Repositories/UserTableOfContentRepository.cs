using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class UserTableOfContentRepository(DataContext context, IMapper mapper) : IUserTableOfContentRepository
{
    public void Attach(AppUserTableOfContent toc)
    {
        context.AppUserTableOfContent.Attach(toc);
    }

    public void Remove(AppUserTableOfContent toc)
    {
        context.AppUserTableOfContent.Remove(toc);
    }

    public async Task<bool> IsUnique(int userId,  int chapterId, int page, string title)
    {
        return await context.AppUserTableOfContent.AnyAsync(t =>
            t.AppUserId == userId && t.PageNumber == page && t.Title == title && t.ChapterId == chapterId);
    }

    public async Task<List<PersonalToCDto>> GetPersonalToC(int userId, int chapterId)
    {
        return await context.AppUserTableOfContent
            .Where(t => t.AppUserId == userId && t.ChapterId == chapterId)
            .ProjectTo<PersonalToCDto>(mapper.ConfigurationProvider)
            .OrderBy(t => t.PageNumber)
            .ToListAsync();
    }

    public async Task<List<PersonalToCDto>> GetPersonalToCForPage(int userId, int chapterId, int page)
    {
        return await context.AppUserTableOfContent
            .Where(t => t.AppUserId == userId && t.ChapterId == chapterId && t.PageNumber == page)
            .ProjectTo<PersonalToCDto>(mapper.ConfigurationProvider)
            .OrderBy(t => t.PageNumber)
            .ToListAsync();
    }

    public async Task<AppUserTableOfContent?> Get(int userId,int chapterId, int pageNum, string title)
    {
        return await context.AppUserTableOfContent
            .Where(t => t.AppUserId == userId && t.ChapterId == chapterId && t.PageNumber == pageNum && t.Title == title)
            .FirstOrDefaultAsync();
    }
}

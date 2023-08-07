using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Reader;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IUserTableOfContentRepository
{
    void Attach(AppUserTableOfContent toc);
    void Remove(AppUserTableOfContent toc);
    Task<bool> IsUnique(int userId, int chapterId, int page, string title);
    IEnumerable<PersonalToCDto> GetPersonalToC(int userId, int chapterId);
    Task<AppUserTableOfContent?> Get(int userId, int chapterId, int pageNum, string title);
}

public class UserTableOfContentRepository : IUserTableOfContentRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public UserTableOfContentRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(AppUserTableOfContent toc)
    {
        _context.AppUserTableOfContent.Attach(toc);
    }

    public void Remove(AppUserTableOfContent toc)
    {
        _context.AppUserTableOfContent.Remove(toc);
    }

    public async Task<bool> IsUnique(int userId,  int chapterId, int page, string title)
    {
        return await _context.AppUserTableOfContent.AnyAsync(t =>
            t.AppUserId == userId && t.PageNumber == page && t.Title == title && t.ChapterId == chapterId);
    }

    public IEnumerable<PersonalToCDto> GetPersonalToC(int userId, int chapterId)
    {
        return _context.AppUserTableOfContent
            .Where(t => t.AppUserId == userId && t.ChapterId == chapterId)
            .ProjectTo<PersonalToCDto>(_mapper.ConfigurationProvider)
            .OrderBy(t => t.PageNumber)
            .AsEnumerable();
    }

    public async Task<AppUserTableOfContent?> Get(int userId,int chapterId, int pageNum, string title)
    {
        return await _context.AppUserTableOfContent
            .Where(t => t.AppUserId == userId && t.ChapterId == chapterId && t.PageNumber == pageNum && t.Title == title)
            .FirstOrDefaultAsync();
    }
}

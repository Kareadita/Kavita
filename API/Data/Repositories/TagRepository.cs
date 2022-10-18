using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface ITagRepository
{
    void Attach(Tag tag);
    void Remove(Tag tag);
    Task<IList<Tag>> GetAllTagsAsync();
    Task<IList<TagDto>> GetAllTagDtosAsync(int userId);
    Task RemoveAllTagNoLongerAssociated(bool removeExternal = false);
    Task<IList<TagDto>> GetAllTagDtosForLibrariesAsync(IList<int> libraryIds, int userId);
}

public class TagRepository : ITagRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public TagRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(Tag tag)
    {
        _context.Tag.Attach(tag);
    }

    public void Remove(Tag tag)
    {
        _context.Tag.Remove(tag);
    }

    public async Task RemoveAllTagNoLongerAssociated(bool removeExternal = false)
    {
        var tagsWithNoConnections = await _context.Tag
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.Chapters)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.Chapters.Count == 0 && p.ExternalTag == removeExternal)
            .AsSplitQuery()
            .ToListAsync();

        _context.Tag.RemoveRange(tagsWithNoConnections);

        await _context.SaveChangesAsync();
    }

    public async Task<IList<TagDto>> GetAllTagDtosForLibrariesAsync(IList<int> libraryIds, int userId)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .SelectMany(s => s.Metadata.Tags)
            .AsSplitQuery()
            .Distinct()
            .OrderBy(t => t.Title)
            .AsNoTracking()
            .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IList<Tag>> GetAllTagsAsync()
    {
        return await _context.Tag.ToListAsync();
    }

    public async Task<IList<TagDto>> GetAllTagDtosAsync(int userId)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.Tag
            .AsNoTracking()
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(t => t.Title)
            .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}

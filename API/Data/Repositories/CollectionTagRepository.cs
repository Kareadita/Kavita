using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.DTOs.CollectionTags;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

[Flags]
public enum CollectionTagIncludes
{
    None = 1,
    SeriesMetadata = 2,
}

public interface ICollectionTagRepository
{
    void Add(CollectionTag tag);
    void Remove(CollectionTag tag);
    Task<IEnumerable<CollectionTagDto>> GetAllTagDtosAsync();
    Task<IEnumerable<CollectionTagDto>> SearchTagDtosAsync(string searchQuery, int userId);
    Task<string?> GetCoverImageAsync(int collectionTagId);
    Task<IEnumerable<CollectionTagDto>> GetAllPromotedTagDtosAsync(int userId);
    Task<CollectionTag?> GetTagAsync(int tagId, CollectionTagIncludes includes = CollectionTagIncludes.None);
    void Update(CollectionTag tag);
    Task<int> RemoveTagsWithoutSeries();
    Task<IEnumerable<CollectionTag>> GetAllTagsAsync(CollectionTagIncludes includes = CollectionTagIncludes.None);

    Task<IEnumerable<CollectionTag>> GetAllTagsByNamesAsync(IEnumerable<string> normalizedTitles,
        CollectionTagIncludes includes = CollectionTagIncludes.None);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<bool> TagExists(string title);
    Task<IList<CollectionTag>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat);
    Task<IList<string>> GetRandomCoverImagesAsync(int collectionId);
}
public class CollectionTagRepository : ICollectionTagRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public CollectionTagRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Add(CollectionTag tag)
    {
        _context.CollectionTag.Add(tag);
    }

    public void Remove(CollectionTag tag)
    {
        _context.CollectionTag.Remove(tag);
    }

    public void Update(CollectionTag tag)
    {
        _context.Entry(tag).State = EntityState.Modified;
    }

    /// <summary>
    /// Removes any collection tags without any series
    /// </summary>
    public async Task<int> RemoveTagsWithoutSeries()
    {
        var tagsToDelete = await _context.CollectionTag
            .Include(c => c.SeriesMetadatas)
            .Where(c => c.SeriesMetadatas.Count == 0)
            .AsSplitQuery()
            .ToListAsync();
        _context.RemoveRange(tagsToDelete);

        return await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<CollectionTag>> GetAllTagsAsync(CollectionTagIncludes includes = CollectionTagIncludes.None)
    {
        return await _context.CollectionTag
            .OrderBy(c => c.NormalizedTitle)
            .Includes(includes)
            .ToListAsync();
    }

    public async Task<IEnumerable<CollectionTag>> GetAllTagsByNamesAsync(IEnumerable<string> normalizedTitles, CollectionTagIncludes includes = CollectionTagIncludes.None)
    {
        return await _context.CollectionTag
            .Where(c => normalizedTitles.Contains(c.NormalizedTitle))
            .OrderBy(c => c.NormalizedTitle)
            .Includes(includes)
            .ToListAsync();
    }

    public async Task<string?> GetCoverImageAsync(int collectionTagId)
    {
        return await _context.CollectionTag
            .Where(c => c.Id == collectionTagId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return (await _context.CollectionTag
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }

    public async Task<bool> TagExists(string title)
    {
        var normalized = title.ToNormalized();
        return await _context.CollectionTag
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized));
    }

    public async Task<IList<CollectionTag>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat)
    {
        var extension = encodeFormat.GetExtension();
        return await _context.CollectionTag
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .ToListAsync();
    }

    public async Task<IList<string>> GetRandomCoverImagesAsync(int collectionId)
    {
        var random = new Random();
        var data = await _context.CollectionTag
            .Where(t => t.Id == collectionId)
            .SelectMany(t => t.SeriesMetadatas)
            .Select(sm => sm.Series.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync();
        return data
            .OrderBy(_ => random.Next())
            .Take(4)
            .ToList();
    }

    public async Task<IEnumerable<CollectionTagDto>> GetAllTagDtosAsync()
    {

        return await _context.CollectionTag
            .OrderBy(c => c.NormalizedTitle)
            .AsNoTracking()
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<CollectionTagDto>> GetAllPromotedTagDtosAsync(int userId)
    {
        var userRating = await GetUserAgeRestriction(userId);
        return await _context.CollectionTag
            .Where(c => c.Promoted)
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(c => c.NormalizedTitle)
            .AsNoTracking()
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }


    public async Task<CollectionTag?> GetTagAsync(int tagId, CollectionTagIncludes includes = CollectionTagIncludes.None)
    {
        return await _context.CollectionTag
            .Where(c => c.Id == tagId)
            .Includes(includes)
            .AsSplitQuery()
            .SingleOrDefaultAsync();
    }

    private async Task<AgeRestriction> GetUserAgeRestriction(int userId)
    {
        return await _context.AppUser
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u =>
                new AgeRestriction(){
                    AgeRating = u.AgeRestriction,
                    IncludeUnknowns = u.AgeRestrictionIncludeUnknowns
                })
            .SingleAsync();
    }

    public async Task<IEnumerable<CollectionTagDto>> SearchTagDtosAsync(string searchQuery, int userId)
    {
        var userRating = await GetUserAgeRestriction(userId);
        return await _context.CollectionTag
            .Where(s => EF.Functions.Like(s.Title!, $"%{searchQuery}%")
                        || EF.Functions.Like(s.NormalizedTitle!, $"%{searchQuery}%"))
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(s => s.NormalizedTitle)
            .AsNoTracking()
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}

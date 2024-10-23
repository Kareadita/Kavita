using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.DTOs.Collection;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Extensions.QueryExtensions.Filtering;
using API.Services.Plus;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

#nullable enable

[Flags]
public enum CollectionTagIncludes
{
    None = 1,
    SeriesMetadata = 2,
    SeriesMetadataWithSeries = 4
}

[Flags]
public enum CollectionIncludes
{
    None = 1,
    Series = 2,
}

public interface ICollectionTagRepository
{
    void Remove(AppUserCollection tag);
    Task<string?> GetCoverImageAsync(int collectionTagId);
    Task<AppUserCollection?> GetCollectionAsync(int tagId, CollectionIncludes includes = CollectionIncludes.None);
    void Update(AppUserCollection tag);
    Task<int> RemoveCollectionsWithoutSeries();

    Task<IEnumerable<AppUserCollection>> GetAllCollectionsAsync(CollectionIncludes includes = CollectionIncludes.None);
    /// <summary>
    /// Returns all of the user's collections with the option of other user's promoted
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="includePromoted"></param>
    /// <returns></returns>
    Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosAsync(int userId, bool includePromoted = false);
    Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosBySeriesAsync(int userId, int seriesId, bool includePromoted = false);

    Task<IList<string>> GetAllCoverImagesAsync();
    Task<bool> CollectionExists(string title, int userId);
    Task<IList<AppUserCollection>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat);
    Task<IList<string>> GetRandomCoverImagesAsync(int collectionId);
    Task<IList<AppUserCollection>> GetCollectionsForUserAsync(int userId, CollectionIncludes includes = CollectionIncludes.None);
    Task UpdateCollectionAgeRating(AppUserCollection tag);
    Task<IEnumerable<AppUserCollection>> GetCollectionsByIds(IEnumerable<int> tags, CollectionIncludes includes = CollectionIncludes.None);
    Task<IList<AppUserCollection>> GetAllCollectionsForSyncing(DateTime expirationTime);
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

    public void Remove(AppUserCollection tag)
    {
        _context.AppUserCollection.Remove(tag);
    }

    public void Update(AppUserCollection tag)
    {
        _context.Entry(tag).State = EntityState.Modified;
    }

    /// <summary>
    /// Removes any collection tags without any series
    /// </summary>
    public async Task<int> RemoveCollectionsWithoutSeries()
    {
        var tagsToDelete = await _context.AppUserCollection
            .Include(c => c.Items)
            .Where(c => c.Items.Count == 0)
            .AsSplitQuery()
            .ToListAsync();

        _context.RemoveRange(tagsToDelete);

        return await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AppUserCollection>> GetAllCollectionsAsync(CollectionIncludes includes = CollectionIncludes.None)
    {
        return await _context.AppUserCollection
            .OrderBy(c => c.NormalizedTitle)
            .Includes(includes)
            .ToListAsync();
    }

    public async Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosAsync(int userId, bool includePromoted = false)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.AppUserCollection
            .Where(uc => uc.AppUserId == userId || (includePromoted && uc.Promoted))
            .WhereIf(ageRating.AgeRating != AgeRating.NotApplicable, uc => uc.AgeRating <= ageRating.AgeRating)
            .OrderBy(uc => uc.Title)
            .ProjectTo<AppUserCollectionDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosBySeriesAsync(int userId, int seriesId, bool includePromoted = false)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.AppUserCollection
            .Where(uc => uc.AppUserId == userId || (includePromoted && uc.Promoted))
            .Where(uc => uc.Items.Any(s => s.Id == seriesId))
            .WhereIf(ageRating.AgeRating != AgeRating.NotApplicable, uc => uc.AgeRating <= ageRating.AgeRating)
            .OrderBy(uc => uc.Title)
            .ProjectTo<AppUserCollectionDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<string?> GetCoverImageAsync(int collectionTagId)
    {
        return await _context.AppUserCollection
            .Where(c => c.Id == collectionTagId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return await _context.AppUserCollection
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync();
    }

    /// <summary>
    /// If any tag exists for that given user's collections
    /// </summary>
    /// <param name="title"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<bool> CollectionExists(string title, int userId)
    {
        var normalized = title.ToNormalized();
        return await _context.AppUserCollection
            .Where(uc => uc.AppUserId == userId)
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized));
    }

    public async Task<IList<AppUserCollection>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat)
    {
        var extension = encodeFormat.GetExtension();
        return await _context.AppUserCollection
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .ToListAsync();
    }

    public async Task<IList<string>> GetRandomCoverImagesAsync(int collectionId)
    {
        var random = new Random();
        var data = await _context.AppUserCollection
            .Where(t => t.Id == collectionId)
            .SelectMany(uc => uc.Items.Select(series => series.CoverImage))
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync();

        return data
            .OrderBy(_ => random.Next())
            .Take(4)
            .ToList();
    }

    public async Task<IList<AppUserCollection>> GetCollectionsForUserAsync(int userId, CollectionIncludes includes = CollectionIncludes.None)
    {
        return await _context.AppUserCollection
            .Where(c => c.AppUserId == userId)
            .Includes(includes)
            .ToListAsync();
    }

    public async Task UpdateCollectionAgeRating(AppUserCollection tag)
    {
        var maxAgeRating = await _context.AppUserCollection
            .Where(t => t.Id == tag.Id)
            .SelectMany(uc => uc.Items.Select(s => s.Metadata))
            .Select(sm => sm.AgeRating)
            .ToListAsync();


        tag.AgeRating = maxAgeRating.Count != 0 ? maxAgeRating.Max() : AgeRating.Unknown;
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AppUserCollection>> GetCollectionsByIds(IEnumerable<int> tags, CollectionIncludes includes = CollectionIncludes.None)
    {
        return await _context.AppUserCollection
            .Where(c => tags.Contains(c.Id))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<IList<AppUserCollection>> GetAllCollectionsForSyncing(DateTime expirationTime)
    {
        return await _context.AppUserCollection
            .Where(c => c.Source == ScrobbleProvider.Mal)
            .Where(c => c.LastSyncUtc <= expirationTime)
            .Include(c => c.Items)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<AppUserCollection?> GetCollectionAsync(int tagId, CollectionIncludes includes = CollectionIncludes.None)
    {
        return await _context.AppUserCollection
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

    public async Task<IEnumerable<AppUserCollectionDto>> SearchTagDtosAsync(string searchQuery, int userId)
    {
        var userRating = await GetUserAgeRestriction(userId);
        return await _context.AppUserCollection
            .Search(searchQuery, userId, userRating)
            .ProjectTo<AppUserCollectionDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}

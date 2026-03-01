using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;



public class CollectionTagRepository(DataContext context, IMapper mapper) : ICollectionTagRepository
{
    public void Remove(AppUserCollection tag)
    {
        context.AppUserCollection.Remove(tag);
    }

    public void Update(AppUserCollection tag)
    {
        context.Entry(tag).State = EntityState.Modified;
    }

    /// <summary>
    /// Removes any collection tags without any series
    /// </summary>
    /// <param name="ct"></param>
    public async Task<int> RemoveCollectionsWithoutSeries(CancellationToken ct = default)
    {
        var tagsToDelete = await context.AppUserCollection
            .Include(c => c.Items)
            .Where(c => c.Items.Count == 0)
            .AsSplitQuery()
            .ToListAsync(ct);

        context.RemoveRange(tagsToDelete);

        return await context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<AppUserCollection>> GetAllCollectionsAsync(
        CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default)
    {
        return await context.AppUserCollection
            .OrderBy(c => c.NormalizedTitle)
            .Includes(includes)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosAsync(int userId,
        bool includePromoted = false, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        return await context.AppUserCollection
            .Where(uc => uc.AppUserId == userId || (includePromoted && uc.Promoted))
            .WhereIf(ageRating.AgeRating != AgeRating.NotApplicable, uc => uc.AgeRating <= ageRating.AgeRating)
            .OrderBy(uc => uc.Title)
            .ProjectTo<AppUserCollectionDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<AppUserCollectionDto?> GetCollectionDtoAsync(int collectionId, int userId, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        return await context.AppUserCollection
            .Where(uc => (uc.AppUserId == userId || uc.Promoted) && uc.Id == collectionId)
            .WhereIf(ageRating.AgeRating != AgeRating.NotApplicable, uc => uc.AgeRating <= ageRating.AgeRating)
            .OrderBy(uc => uc.Title)
            .ProjectTo<AppUserCollectionDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedList<AppUserCollectionDto>> GetCollectionDtosPagedAsync(int userId, UserParams userParams,
        bool includePromoted = false, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var collections = context.AppUserCollection
            .Where(uc => uc.AppUserId == userId || (includePromoted && uc.Promoted))
            .WhereIf(ageRating.AgeRating != AgeRating.NotApplicable, uc => uc.AgeRating <= ageRating.AgeRating)
            .OrderBy(uc => uc.Title)
            .ProjectTo<AppUserCollectionDto>(mapper.ConfigurationProvider);

        return await PagedList<AppUserCollectionDto>.CreateAsync(collections, userParams, ct);
    }

    public async Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosBySeriesAsync(int userId, int seriesId,
        bool includePromoted = false, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        return await context.AppUserCollection
            .Where(uc => uc.AppUserId == userId || (includePromoted && uc.Promoted))
            .Where(uc => uc.Items.Any(s => s.Id == seriesId))
            .WhereIf(ageRating.AgeRating != AgeRating.NotApplicable, uc => uc.AgeRating <= ageRating.AgeRating)
            .OrderBy(uc => uc.Title)
            .ProjectTo<AppUserCollectionDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<string?> GetCoverImageAsync(int collectionTagId, CancellationToken ct = default)
    {
        return await context.AppUserCollection
            .Where(c => c.Id == collectionTagId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default)
    {
        return await context.AppUserCollection
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct);
    }

    /// <summary>
    /// If any tag exists for that given user's collections
    /// </summary>
    /// <param name="title"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> CollectionExists(string title, int userId, CancellationToken ct = default)
    {
        var normalized = title.ToNormalized();
        return await context.AppUserCollection
            .Where(uc => uc.AppUserId == userId)
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized), ct);
    }

    public async Task<IList<AppUserCollection>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat,
        CancellationToken ct = default)
    {
        var extension = encodeFormat.GetExtension();
        return await context.AppUserCollection
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .ToListAsync(ct);
    }

    public async Task<IList<string>> GetRandomCoverImagesAsync(int collectionId, CancellationToken ct = default)
    {
        var random = new Random();
        var data = await context.AppUserCollection
            .Where(t => t.Id == collectionId)
            .SelectMany(uc => uc.Items.Select(series => series.CoverImage))
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct);

        return data
            .OrderBy(_ => random.Next())
            .Take(4)
            .ToList();
    }

    public async Task<IList<AppUserCollection>> GetCollectionsForUserAsync(int userId,
        CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default)
    {
        return await context.AppUserCollection
            .Where(c => c.AppUserId == userId)
            .Includes(includes)
            .ToListAsync(ct);
    }

    public async Task UpdateCollectionAgeRating(AppUserCollection tag, CancellationToken ct = default)
    {
        var maxAgeRating = await context.AppUserCollection
            .Where(t => t.Id == tag.Id)
            .SelectMany(uc => uc.Items.Select(s => s.Metadata))
            .Select(sm => sm.AgeRating)
            .ToListAsync(ct);


        tag.AgeRating = maxAgeRating.Count != 0 ? maxAgeRating.Max() : AgeRating.Unknown;
        await context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<AppUserCollection>> GetCollectionsByIds(IEnumerable<int> tags,
        CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default)
    {
        return await context.AppUserCollection
            .Where(c => tags.Contains(c.Id))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<IList<AppUserCollection>> GetAllCollectionsForSyncing(DateTime expirationTime,
        CancellationToken ct = default)
    {
        return await context.AppUserCollection
            .Where(c => c.Source == ScrobbleProvider.Mal)
            .Where(c => c.LastSyncUtc <= expirationTime)
            .Include(c => c.Items)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<AppUserCollection?> GetCollectionAsync(int tagId,
        CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default)
    {
        return await context.AppUserCollection
            .Where(c => c.Id == tagId)
            .Includes(includes)
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);
    }

    private async Task<AgeRestriction> GetUserAgeRestriction(int userId, CancellationToken ct = default)
    {
        return await context.AppUser
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u =>
                new AgeRestriction(){
                    AgeRating = u.AgeRestriction,
                    IncludeUnknowns = u.AgeRestrictionIncludeUnknowns
                })
            .SingleAsync(ct);
    }

    public async Task<IEnumerable<AppUserCollectionDto>> SearchTagDtosAsync(string searchQuery, int userId, CancellationToken ct = default)
    {
        var userRating = await GetUserAgeRestriction(userId, ct);
        return await context.AppUserCollection
            .Search(searchQuery, userId, userRating)
            .ProjectTo<AppUserCollectionDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }
}

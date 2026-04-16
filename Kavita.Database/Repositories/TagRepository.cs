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
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Metadata.Browse;
using Kavita.Models.Entities;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class TagRepository(DataContext context, IMapper mapper) : ITagRepository
{
    public void Attach(Tag tag)
    {
        context.Tag.Attach(tag);
    }

    public void Remove(Tag tag)
    {
        context.Tag.Remove(tag);
    }

    public async Task RemoveAllTagNoLongerAssociated(CancellationToken ct = default)
    {
        var tagsWithNoConnections = await context.Tag
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.Chapters)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.Chapters.Count == 0)
            .AsSplitQuery()
            .ToListAsync(ct);

        context.Tag.RemoveRange(tagsWithNoConnections);

        await context.SaveChangesAsync(ct);
    }

    public async Task<IList<TagDto>> GetAllTagDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null,
        CancellationToken ct = default)
    {
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync(ct);

        if (libraryIds is {Count: > 0})
        {
            userLibs = userLibs.Where(libraryIds.Contains).ToList();
        }

        return await context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .SelectMany(s => s.Metadata.Tags)
            .AsSplitQuery()
            .Distinct()
            .OrderBy(t => t.NormalizedTitle)
            .AsNoTracking()
            .ProjectTo<TagDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetAllTagsNotInListAsync(ICollection<string> tags, CancellationToken ct = default)
    {
        // Create a dictionary mapping normalized names to non-normalized names
        var normalizedToOriginalMap = tags.Distinct()
            .GroupBy(t => t.ToNormalized())
            .ToDictionary(group => group.Key, group => group.First());

        var normalizedTagNames = normalizedToOriginalMap.Keys.ToList();

        // Query the database for existing genres using the normalized names
        var existingTags = await context.Tag
            .Where(g => normalizedTagNames.Contains(g.NormalizedTitle)) // Assuming you have a normalized field
            .Select(g => g.NormalizedTitle)
            .ToListAsync(ct);

        // Find the normalized genres that do not exist in the database
        var missingTags = normalizedTagNames.Except(existingTags).ToList();

        // Return the original non-normalized genres for the missing ones
        return missingTags.Select(normalizedName => normalizedToOriginalMap[normalizedName]).ToList();
    }

    public async Task<PagedList<BrowseTagDto>> GetBrowseableTag(int userId, UserParams userParams,
        CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        var allLibrariesCount = await context.Library.CountAsync(ct);
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync(ct);

        var seriesIds = context.Series.Where(s => userLibs.Contains(s.LibraryId)).Select(s => s.Id);

        var query = context.Tag
            .RestrictAgainstAgeRestriction(ageRating)
            .WhereIf(userLibs.Count != allLibrariesCount,
                tag => tag.Chapters.Any(cp => seriesIds.Contains(cp.Volume.SeriesId)) ||
                       tag.SeriesMetadatas.Any(sm => seriesIds.Contains(sm.SeriesId)))
            .Select(g => new BrowseTagDto
            {
                Id = g.Id,
                Title = g.Title,
                SeriesCount = g.SeriesMetadatas
                    .Where(sm => allLibrariesCount == userLibs.Count || seriesIds.Contains(sm.SeriesId))
                    .RestrictAgainstAgeRestriction(ageRating)
                    .Distinct()
                    .Count(),
                ChapterCount = g.Chapters
                    .Where(ch => allLibrariesCount == userLibs.Count || seriesIds.Contains(ch.Volume.SeriesId))
                    .RestrictAgainstAgeRestriction(ageRating)
                    .Distinct()
                    .Count()
            })
            .OrderBy(g => g.Title);

        return await PagedList<BrowseTagDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }


    public async Task<IList<Tag>> GetAllTagsByNameAsync(IEnumerable<string> normalizedNames, CancellationToken ct = default)
    {
        return await context.Tag
            .Where(t => normalizedNames.Contains(t.NormalizedTitle))
            .ToListAsync(ct);
    }
}

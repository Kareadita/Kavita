using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.JumpBar;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;



public class LibraryRepository(DataContext context, IMapper mapper) : ILibraryRepository
{
    public void Add(Library library)
    {
        context.Library.Add(library);
    }

    public void Update(Library library)
    {
        context.Entry(library).State = EntityState.Modified;
    }

    public void Delete(Library? library)
    {
        if (library == null) return;
        context.Library.Remove(library);
    }

    public async Task<IList<LibraryDto>> GetLibraryDtosForUsernameAsync(string userName, CancellationToken ct = default)
    {
        return await context.Library
            .Include(l => l.AppUsers)
            .Include(l => l.LibraryFileTypes)
            .Include(l => l.LibraryExcludePatterns)
            .Where(library => library.AppUsers.Any(x => x.UserName!.Equals(userName)))
            .OrderBy(l => l.Name)
            .ProjectTo<LibraryDto>(mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns all libraries including their AppUsers + extra includes
    /// </summary>
    /// <param name="includes"></param>
    /// <param name="track"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Library>> GetLibrariesAsync(LibraryIncludes includes = LibraryIncludes.None,
        bool track = true, CancellationToken ct = default)
    {
        var query = context.Library
            .Include(l => l.AppUsers)
            .Includes(includes)
            .AsSplitQuery();

        if (track) return await query.ToListAsync(ct);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    /// <summary>
    /// This does not track
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Library>> GetLibrariesForUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await context.Library
            .Include(l => l.AppUsers)
            .Where(l => l.AppUsers.Select(ap => ap.Id).Contains(userId))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IList<int>> GetLibraryIdsForUserIdAsync(int userId, QueryContext queryContext = QueryContext.None,
        CancellationToken ct = default)
    {
        return await context.Library
            .IsRestricted(queryContext)
            .Where(l => l.AppUsers.Select(ap => ap.Id).Contains(userId))
            .Select(l => l.Id)
            .ToListAsync(ct);
    }

    public async Task<LibraryType> GetLibraryTypeAsync(int libraryId, CancellationToken ct = default)
    {
        return await context.Library
            .Where(l => l.Id == libraryId)
            .AsNoTracking()
            .Select(l => l.Type)
            .FirstAsync(ct);
    }

    public async Task<LibraryType> GetLibraryTypeBySeriesIdAsync(int seriesId, CancellationToken ct = default)
    {
        return await context.Series
            .Where(s => s.Id == seriesId)
            .Select(s => s.Library.Type)
            .FirstAsync(ct);
    }

    public async Task<IEnumerable<Library>> GetLibraryForIdsAsync(IEnumerable<int> libraryIds,
        LibraryIncludes includes = LibraryIncludes.None, CancellationToken ct = default)
    {
        return await context.Library
            .Where(x => libraryIds.Contains(x.Id))
            .Includes(includes)
            .ToListAsync(ct);
    }


    public IEnumerable<JumpKeyDto> GetJumpBarAsync(int libraryId, CancellationToken ct = default)
    {
        var seriesSortCharacters = context.Series.Where(s => s.LibraryId == libraryId)
            .Select(s => s.SortName!.ToUpper())
            .OrderBy(s => s)
            .AsEnumerable()
            .Select(s => s[0]);

        // Map the title to the number of entities
        var firstCharacterMap = new Dictionary<char, int>();
        foreach (var sortChar in seriesSortCharacters)
        {
            var c = sortChar;
            var isAlpha = char.IsLetter(sortChar);
            if (!isAlpha) c = '#';
            firstCharacterMap.TryAdd(c, 0);

            firstCharacterMap[c] += 1;
        }

        return firstCharacterMap.Keys.Select(k => new JumpKeyDto()
        {
            Key = k + string.Empty,
            Size = firstCharacterMap[k],
            Title = k + string.Empty
        });
    }

    /// <summary>
    /// Returns all Libraries with their Folders
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync(CancellationToken ct = default)
    {
        return await context.Library
            .Include(f => f.Folders)
            .Include(l => l.LibraryFileTypes)
            .OrderBy(l => l.Name)
            .ProjectTo<LibraryDto>(mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<LibraryDto?> GetLibraryDtoByIdAsync(int libraryId, CancellationToken ct = default)
    {
        return await context.Library
            .Include(f => f.Folders)
            .Include(l => l.LibraryFileTypes)
            .ProjectTo<LibraryDto>(mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == libraryId, ct);
    }

    public async Task<LiteLibraryDto?> GetLiteLibraryDtoByIdAsync(int libraryId, CancellationToken ct = default)
    {
        return await context.Library
            .ProjectTo<LiteLibraryDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(l => l.Id == libraryId, ct);
    }

    public async Task<Library?> GetLibraryForIdAsync(int libraryId, LibraryIncludes includes = LibraryIncludes.None,
        CancellationToken ct = default)
    {

        var query = context.Library
            .Where(x => x.Id == libraryId)
            .Includes(includes);

        return await query.SingleOrDefaultAsync(ct);
    }


    public async Task<bool> LibraryExists(string libraryName, CancellationToken ct = default)
    {
        return await context.Library
            .AsNoTracking()
            .AnyAsync(x => x.Name != null && x.Name.Equals(libraryName), ct);
    }


    public async Task<IList<AgeRatingDto>> GetAllAgeRatingsDtosForLibrariesAsync(List<int> libraryIds,
        CancellationToken ct = default)
    {
        return await context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Metadata.AgeRating)
            .Distinct()
            .Select(s => new AgeRatingDto()
            {
                Value = s,
                Title = s.ToDescription()
            })
            .ToListAsync(ct);
    }

    public async Task<IList<LanguageDto>> GetAllLanguagesForLibrariesAsync(List<int>? libraryIds,
        CancellationToken ct = default)
    {
        var ret = await context.Series
            .WhereIf(libraryIds is {Count: > 0} , s => libraryIds!.Contains(s.LibraryId))
            .Select(s => s.Metadata.Language)
            .AsSplitQuery()
            .AsNoTracking()
            .Distinct()
            .ToListAsync(ct);

        return ret
            .Where(s => !string.IsNullOrEmpty(s))
            .DistinctBy(l => l.ToNormalized())
            .Select(GetCulture)
            .OrderBy(s => s.Title)
            .ToList();
    }

    private static LanguageDto GetCulture(string s)
    {
        try
        {
            return new LanguageDto()
            {
                Title = CultureInfo.GetCultureInfo(s).DisplayName,
                IsoCode = s
            };
        }
        catch (Exception)
        {
            // ignored
        }

        return new LanguageDto()
        {
            Title = s,
            IsoCode = s
        };
    }

    public IEnumerable<PublicationStatusDto> GetAllPublicationStatusesDtosForLibrariesAsync(List<int> libraryIds,
        CancellationToken ct = default)
    {
        return  context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .AsSplitQuery()
            .Select(s => s.Metadata.PublicationStatus)
            .Distinct()
            .AsEnumerable()
            .Select(s => new PublicationStatusDto()
            {
                Value = s,
                Title = s.ToDescription()
            })
            .OrderBy(s => s.Title);
    }

    /// <summary>
    /// Checks if any series folders match the folders passed in
    /// </summary>
    /// <param name="folders"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> DoAnySeriesFoldersMatch(IEnumerable<string> folders, CancellationToken ct = default)
    {
        var normalized = folders.Select(f => f.NormalizePath());
        return await context.Series.AnyAsync(s => normalized.Contains(s.FolderPath), ct);
    }

    public Task<string?> GetLibraryCoverImageAsync(int libraryId, CancellationToken ct = default)
    {
        return context.Library
            .Where(l => l.Id == libraryId)
            .Select(l => l.CoverImage)
            .SingleOrDefaultAsync(ct);

    }

    public async Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default)
    {
        return (await context.ReadingList
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct))!;
    }

    public async Task<IList<Library>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat,
        CancellationToken ct = default)
    {
        var extension = encodeFormat.GetExtension();
        return await context.Library
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .ToListAsync(ct);
    }

    public async Task<bool> GetAllowsScrobblingBySeriesId(int seriesId, CancellationToken ct = default)
    {
        return await context.Series.Where(s => s.Id == seriesId)
            .Select(s => s.Library.AllowScrobbling)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IDictionary<int, LibraryType>> GetLibraryTypesBySeriesIdsAsync(IList<int> seriesIds,
        CancellationToken ct = default)
    {
        return await context.Series
            .Where(series => seriesIds.Contains(series.Id))
            .Select(series => new
            {
                series.Id,
                series.Library.Type
            })
            .ToDictionaryAsync(entity => entity.Id, entity => entity.Type, ct);
    }
}

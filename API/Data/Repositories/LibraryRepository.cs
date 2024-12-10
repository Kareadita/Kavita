using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.JumpBar;
using API.DTOs.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.Common.Extensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

[Flags]
public enum LibraryIncludes
{
    None = 1,
    Series = 2,
    AppUser = 4,
    Folders = 8,
    FileTypes = 16,
    ExcludePatterns = 32
}

public interface ILibraryRepository
{
    void Add(Library library);
    void Update(Library library);
    void Delete(Library? library);
    Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync();
    Task<bool> LibraryExists(string libraryName);
    Task<Library?> GetLibraryForIdAsync(int libraryId, LibraryIncludes includes = LibraryIncludes.None);
    IEnumerable<LibraryDto> GetLibraryDtosForUsernameAsync(string userName);
    Task<IEnumerable<Library>> GetLibrariesAsync(LibraryIncludes includes = LibraryIncludes.None, bool track = true);
    Task<IEnumerable<Library>> GetLibrariesForUserIdAsync(int userId);
    IEnumerable<int> GetLibraryIdsForUserIdAsync(int userId, QueryContext queryContext = QueryContext.None);
    Task<LibraryType> GetLibraryTypeAsync(int libraryId);
    Task<LibraryType> GetLibraryTypeBySeriesIdAsync(int seriesId);
    Task<IEnumerable<Library>> GetLibraryForIdsAsync(IEnumerable<int> libraryIds, LibraryIncludes includes = LibraryIncludes.None);
    Task<int> GetTotalFiles();
    IEnumerable<JumpKeyDto> GetJumpBarAsync(int libraryId);
    Task<IList<AgeRatingDto>> GetAllAgeRatingsDtosForLibrariesAsync(List<int> libraryIds);
    Task<IList<LanguageDto>> GetAllLanguagesForLibrariesAsync(List<int>? libraryIds);
    IEnumerable<PublicationStatusDto> GetAllPublicationStatusesDtosForLibrariesAsync(List<int> libraryIds);
    Task<bool> DoAnySeriesFoldersMatch(IEnumerable<string> folders);
    Task<string?> GetLibraryCoverImageAsync(int libraryId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<IList<Library>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat);
    Task<bool> GetAllowsScrobblingBySeriesId(int seriesId);

    Task<IDictionary<int, LibraryType>> GetLibraryTypesBySeriesIdsAsync(IList<int> seriesIds);
}

public class LibraryRepository : ILibraryRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public LibraryRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Add(Library library)
    {
        _context.Library.Add(library);
    }

    public void Update(Library library)
    {
        _context.Entry(library).State = EntityState.Modified;
    }

    public void Delete(Library? library)
    {
        if (library == null) return;
        _context.Library.Remove(library);
    }

    public IEnumerable<LibraryDto> GetLibraryDtosForUsernameAsync(string userName)
    {
        return _context.Library
            .Include(l => l.AppUsers)
            .Include(l => l.LibraryFileTypes)
            .Include(l => l.LibraryExcludePatterns)
            .Where(library => library.AppUsers.Any(x => x.UserName!.Equals(userName)))
            .OrderBy(l => l.Name)
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsEnumerable();
    }

    /// <summary>
    /// Returns all libraries including their AppUsers + extra includes
    /// </summary>
    /// <param name="includes"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Library>> GetLibrariesAsync(LibraryIncludes includes = LibraryIncludes.None, bool track = true)
    {
        var query = _context.Library
            .Include(l => l.AppUsers)
            .Includes(includes)
            .AsSplitQuery();

        if (track) return await query.ToListAsync();

        return await query.AsNoTracking().ToListAsync();
    }

    /// <summary>
    /// This does not track
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Library>> GetLibrariesForUserIdAsync(int userId)
    {
        return await _context.Library
            .Include(l => l.AppUsers)
            .Where(l => l.AppUsers.Select(ap => ap.Id).Contains(userId))
            .AsNoTracking()
            .ToListAsync();
    }

    public IEnumerable<int> GetLibraryIdsForUserIdAsync(int userId, QueryContext queryContext = QueryContext.None)
    {
        return _context.Library
            .IsRestricted(queryContext)
            .Where(l => l.AppUsers.Select(ap => ap.Id).Contains(userId))
            .Select(l => l.Id)
            .AsEnumerable();
    }

    public async Task<LibraryType> GetLibraryTypeAsync(int libraryId)
    {
        return await _context.Library
            .Where(l => l.Id == libraryId)
            .AsNoTracking()
            .Select(l => l.Type)
            .FirstAsync();
    }

    public async Task<LibraryType> GetLibraryTypeBySeriesIdAsync(int seriesId)
    {
        return await _context.Series
            .Where(s => s.Id == seriesId)
            .Select(s => s.Library.Type)
            .FirstAsync();
    }

    public async Task<IEnumerable<Library>> GetLibraryForIdsAsync(IEnumerable<int> libraryIds, LibraryIncludes includes = LibraryIncludes.None)
    {
        return await _context.Library
            .Where(x => libraryIds.Contains(x.Id))
            .Includes(includes)
            .ToListAsync();
    }

    public async Task<int> GetTotalFiles()
    {
        return await _context.MangaFile.CountAsync();
    }

    public IEnumerable<JumpKeyDto> GetJumpBarAsync(int libraryId)
    {
        var seriesSortCharacters = _context.Series.Where(s => s.LibraryId == libraryId)
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
    /// <returns></returns>
    public async Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync()
    {
        return await _context.Library
            .Include(f => f.Folders)
            .Include(l => l.LibraryFileTypes)
            .OrderBy(l => l.Name)
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Library?> GetLibraryForIdAsync(int libraryId, LibraryIncludes includes = LibraryIncludes.None)
    {

        var query = _context.Library
            .Where(x => x.Id == libraryId)
            .Includes(includes);

        return await query.SingleOrDefaultAsync();
    }


    public async Task<bool> LibraryExists(string libraryName)
    {
        return await _context.Library
            .AsNoTracking()
            .AnyAsync(x => x.Name != null && x.Name.Equals(libraryName));
    }

    public async Task<IEnumerable<LibraryDto>> GetLibrariesForUserAsync(AppUser user)
    {
        return await _context.Library
            .Where(library => library.AppUsers.Contains(user))
            .Include(l => l.Folders)
            .AsNoTracking()
            .AsSplitQuery()
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }


    public async Task<IList<AgeRatingDto>> GetAllAgeRatingsDtosForLibrariesAsync(List<int> libraryIds)
    {
        return await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Metadata.AgeRating)
            .Distinct()
            .Select(s => new AgeRatingDto()
            {
                Value = s,
                Title = s.ToDescription()
            })
            .ToListAsync();
    }

    public async Task<IList<LanguageDto>> GetAllLanguagesForLibrariesAsync(List<int>? libraryIds)
    {
        var ret = await _context.Series
            .WhereIf(libraryIds is {Count: > 0} , s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Metadata.Language)
            .AsSplitQuery()
            .AsNoTracking()
            .Distinct()
            .ToListAsync();

        return ret
            .Where(s => !string.IsNullOrEmpty(s))
            .DistinctBy(Parser.Normalize)
            .Select(GetCulture)
            .Where(s => s != null)
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

    public IEnumerable<PublicationStatusDto> GetAllPublicationStatusesDtosForLibrariesAsync(List<int> libraryIds)
    {
        return  _context.Series
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
    /// <returns></returns>
    public async Task<bool> DoAnySeriesFoldersMatch(IEnumerable<string> folders)
    {
        var normalized = folders.Select(Parser.NormalizePath);
        return await _context.Series.AnyAsync(s => normalized.Contains(s.FolderPath));
    }

    public Task<string?> GetLibraryCoverImageAsync(int libraryId)
    {
        return _context.Library
            .Where(l => l.Id == libraryId)
            .Select(l => l.CoverImage)
            .SingleOrDefaultAsync();

    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return (await _context.ReadingList
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }

    public async Task<IList<Library>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat)
    {
        var extension = encodeFormat.GetExtension();
        return await _context.Library
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .ToListAsync();
    }

    public async Task<bool> GetAllowsScrobblingBySeriesId(int seriesId)
    {
        return await _context.Series.Where(s => s.Id == seriesId)
            .Select(s => s.Library.AllowScrobbling)
            .SingleOrDefaultAsync();
    }

    public async Task<IDictionary<int, LibraryType>> GetLibraryTypesBySeriesIdsAsync(IList<int> seriesIds)
    {
        return await _context.Series
            .Where(series => seriesIds.Contains(series.Id))
            .Select(series => new
            {
                series.Id,
                series.Library.Type
            })
            .ToDictionaryAsync(entity => entity.Id, entity => entity.Type);
    }
}

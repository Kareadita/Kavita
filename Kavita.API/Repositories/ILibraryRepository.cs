using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.JumpBar;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Repositories;

[Flags]
public enum LibraryIncludes
{
    None = 1 << 0,
    Series = 1 << 1,
    AppUser = 1 << 2,
    Folders = 1 << 3,
    FileTypes = 1 << 4,
    ExcludePatterns = 1 << 5
}

public interface ILibraryRepository
{
    void Add(Library library);
    void Update(Library library);
    void Delete(Library? library);
    Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync(CancellationToken ct = default);
    Task<LibraryDto?> GetLibraryDtoByIdAsync(int libraryId, CancellationToken ct = default);
    Task<LiteLibraryDto?> GetLiteLibraryDtoByIdAsync(int libraryId, CancellationToken ct = default);
    Task<bool> LibraryExists(string libraryName, CancellationToken ct = default);
    Task<Library?> GetLibraryForIdAsync(int libraryId, LibraryIncludes includes = LibraryIncludes.None, CancellationToken ct = default);
    Task<IList<LibraryDto>> GetLibraryDtosForUsernameAsync(string userName, CancellationToken ct = default);
    Task<IEnumerable<Library>> GetLibrariesAsync(LibraryIncludes includes = LibraryIncludes.None, bool track = true, CancellationToken ct = default);
    Task<IEnumerable<Library>> GetLibrariesForUserIdAsync(int userId, CancellationToken ct = default);
    Task<IList<int>> GetLibraryIdsForUserIdAsync(int userId, QueryContext queryContext = QueryContext.None, CancellationToken ct = default);
    Task<LibraryType> GetLibraryTypeAsync(int libraryId, CancellationToken ct = default);
    Task<LibraryType> GetLibraryTypeBySeriesIdAsync(int seriesId, CancellationToken ct = default);
    Task<IEnumerable<Library>> GetLibraryForIdsAsync(IEnumerable<int> libraryIds, LibraryIncludes includes = LibraryIncludes.None, CancellationToken ct = default);
    IEnumerable<JumpKeyDto> GetJumpBarAsync(int libraryId, CancellationToken ct = default);
    Task<IList<AgeRatingDto>> GetAllAgeRatingsDtosForLibrariesAsync(List<int> libraryIds, CancellationToken ct = default);
    Task<IList<LanguageDto>> GetAllLanguagesForLibrariesAsync(List<int>? libraryIds, CancellationToken ct = default);
    IEnumerable<PublicationStatusDto> GetAllPublicationStatusesDtosForLibrariesAsync(List<int> libraryIds, CancellationToken ct = default);
    Task<bool> DoAnySeriesFoldersMatch(IEnumerable<string> folders, CancellationToken ct = default);
    Task<string?> GetLibraryCoverImageAsync(int libraryId, CancellationToken ct = default);
    Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default);
    Task<IList<Library>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat, CancellationToken ct = default);
    Task<bool> GetAllowsScrobblingBySeriesId(int seriesId, CancellationToken ct = default);

    Task<IDictionary<int, LibraryType>> GetLibraryTypesBySeriesIdsAsync(IList<int> seriesIds, CancellationToken ct = default);
}

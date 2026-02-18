using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Metadata.Browse;
using Kavita.Models.Entities;

namespace Kavita.API.Repositories;

public interface IGenreRepository
{
    void Attach(Genre genre);
    void Remove(Genre genre);
    Task<Genre?> FindByNameAsync(string genreName, CancellationToken ct = default);
    Task<IList<Genre>> GetAllGenresAsync(CancellationToken ct = default);
    Task<IList<Genre>> GetAllGenresByNamesAsync(IEnumerable<string> normalizedNames, CancellationToken ct = default);
    Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false, CancellationToken ct = default);
    Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null, QueryContext context = QueryContext.None, CancellationToken ct = default);
    Task<int> GetCountAsync(CancellationToken ct = default);
    Task<GenreTagDto?> GetRandomGenre(CancellationToken ct = default);
    Task<GenreTagDto?> GetGenreById(int id, CancellationToken ct = default);
    Task<List<string>> GetAllGenresNotInListAsync(ICollection<string> genreNames, CancellationToken ct = default);
    Task<PagedList<BrowseGenreDto>> GetBrowseableGenre(int userId, UserParams userParams, CancellationToken ct = default);
}

using System.Collections.Generic;
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
    Task<Genre?> FindByNameAsync(string genreName);
    Task<IList<Genre>> GetAllGenresAsync();
    Task<IList<Genre>> GetAllGenresByNamesAsync(IEnumerable<string> normalizedNames);
    Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false);
    Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null, QueryContext context = QueryContext.None);
    Task<int> GetCountAsync();
    Task<GenreTagDto?> GetRandomGenre();
    Task<GenreTagDto?> GetGenreById(int id);
    Task<List<string>> GetAllGenresNotInListAsync(ICollection<string> genreNames);
    Task<PagedList<BrowseGenreDto>> GetBrowseableGenre(int userId, UserParams userParams);
}

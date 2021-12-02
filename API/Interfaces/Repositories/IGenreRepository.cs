using System.Collections.Generic;
using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IGenreRepository
    {
        void Attach(Genre genre);
        void Remove(Genre genre);
        Task<Genre> FindByNameAsync(string genreName);
        Task<IList<Genre>> GetAllGenres();
        Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false);
    }
}

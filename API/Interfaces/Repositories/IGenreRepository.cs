using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IGenreRepository
    {
        void Add(Genre genre);
        void Remove(Genre genre);
        Task<Genre> FindByNameAsync(string genreName);
    }
}

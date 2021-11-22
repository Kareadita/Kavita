using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IPersonRepository
    {
        void Add(Person genre);
        void Remove(Person genre);
        // TODO: Put a filter here
        Task<Person> FindByNameAsync(string name);
    }
}

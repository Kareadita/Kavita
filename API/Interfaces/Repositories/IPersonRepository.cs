using System.Collections.Generic;
using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IPersonRepository
    {
        void Attach(Person person);
        void Remove(Person person);
        // TODO: Put a filter here
        Task<Person> FindByNameAsync(string name);
        Task<IList<Person>> GetAllPeople();
    }
}

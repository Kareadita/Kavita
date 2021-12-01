using System.Collections.Generic;
using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IPersonRepository
    {
        void Attach(Person person);
        void Remove(Person person);
        Task<IList<Person>> GetAllPeople();
        Task RemoveAllPeopleNoLongerAssociated(bool removeExternal = false);
    }
}

using System.Threading.Tasks;
using API.Data;
using API.Entities.Person;

namespace API.Services;

public interface IPersonService
{
    /// <summary>
    /// Adds src as an alias to dst, this is a destructive operation
    /// </summary>
    /// <param name="dst">Remaining person</param>
    /// <param name="src">Merged person</param>
    /// <returns></returns>
    Task MergePeopleAsync(Person dst, Person src);
}

public class PersonService(IUnitOfWork unitOfWork): IPersonService
{

    public async Task MergePeopleAsync(Person dst, Person src)
    {
        throw new System.NotImplementedException();
    }
}

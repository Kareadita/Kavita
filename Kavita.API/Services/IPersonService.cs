using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Person;

namespace Kavita.API.Services;

public interface IPersonService
{
    /// <summary>
    /// Adds src as an alias to dst, this is a destructive operation
    /// </summary>
    /// <param name="src">Merged person</param>
    /// <param name="dst">Remaining person</param>
    /// <param name="ct"></param>
    /// <remarks>The entities passed as arguments **must** include all relations</remarks>
    /// <returns></returns>
    Task MergePeopleAsync(Person src, Person dst, CancellationToken ct = default);

    /// <summary>
    /// Adds the alias to the person, requires that the aliases are not shared with anyone else
    /// </summary>
    /// <remarks>This method does NOT commit changes</remarks>
    /// <param name="person"></param>
    /// <param name="aliases"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> UpdatePersonAliasesAsync(Person person, IList<string> aliases, CancellationToken ct = default);
}

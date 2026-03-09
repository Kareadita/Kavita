using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Metadata.Browse;
using Kavita.Models.DTOs.Metadata.Browse.Requests;
using Kavita.Models.DTOs.Person;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;

namespace Kavita.API.Repositories;

[Flags]
public enum PersonIncludes
{
    None = 1 << 0,
    Aliases = 1 << 1,
    ChapterPeople = 1 << 2,
    SeriesPeople = 1 << 3,

    All = Aliases | ChapterPeople | SeriesPeople,
}

public interface IPersonRepository
{
    void Attach(Person person);
    void Attach(IEnumerable<Person> person);
    void Remove(Person person);
    void Remove(ChapterPeople person);
    void Remove(SeriesMetadataPeople person);
    void Update(Person person);

    Task<IList<Person>> GetAllPeople(PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default);
    Task<IList<PersonDto>> GetAllPersonDtosAsync(int userId, PersonIncludes includes = PersonIncludes.None, CancellationToken ct = default);
    Task<IList<PersonDto>> GetAllPersonDtosByRoleAsync(int userId, PersonRole role, PersonIncludes includes = PersonIncludes.None, CancellationToken ct = default);
    Task RemoveAllPeopleNoLongerAssociated(CancellationToken ct = default);
    Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(int userId, List<int>? libraryIds = null, PersonIncludes includes = PersonIncludes.None, CancellationToken ct = default);

    Task<string?> GetCoverImageAsync(int personId, CancellationToken ct = default);
    Task<IList<string?>> GetAllCoverImagesAsync(CancellationToken ct = default);
    Task<string?> GetCoverImageByNameAsync(string name, CancellationToken ct = default);
    Task<IEnumerable<PersonRole>> GetRolesForPersonByName(int personId, int userId, CancellationToken ct = default);
    Task<PagedList<BrowsePersonDto>> GetBrowsePersonDtos(int userId, BrowsePersonFilterDto filter, UserParams userParams, CancellationToken ct = default);
    Task<Person?> GetPersonById(int personId, PersonIncludes includes = PersonIncludes.None, CancellationToken ct = default);
    Task<PersonDto?> GetPersonDtoByName(string name, int userId, PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default);

    /// <summary>
    /// Returns a person matched on a normalized name or alias
    /// </summary>
    /// <param name="name"></param>
    /// <param name="includes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<Person?> GetPersonByNameOrAliasAsync(string name, PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default);
    Task<bool> IsNameUnique(string name, CancellationToken ct = default);

    Task<IEnumerable<SeriesDto>> GetSeriesKnownFor(int personId, int userId, CancellationToken ct = default);
    Task<IEnumerable<StandaloneChapterDto>> GetChaptersForPersonByRole(int personId, int userId, PersonRole role, CancellationToken ct = default);

    /// <summary>
    /// Returns all people with a matching name, or alias
    /// </summary>
    /// <param name="normalizedNames"></param>
    /// <param name="includes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<Person>> GetPeopleByNames(List<string> normalizedNames, PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default);
    Task<Person?> GetPersonByAniListId(int aniListId, PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default);

    Task<IList<PersonDto>> SearchPeople(string searchQuery, PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default);

    Task<bool> AnyAliasExist(string alias, CancellationToken ct = default);
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Person;
using API.Extensions;

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

    /// <summary>
    /// Adds the alias to the person, requires that the alias is not shared with anyone else
    /// </summary>
    /// <remarks>This method does NOT commit changes</remarks>
    /// <param name="person"></param>
    /// <param name="aliases"></param>
    /// <returns></returns>
    Task<bool> UpdatePersonAliasesAsync(Person person, IList<string> aliases);
}

public class PersonService(IUnitOfWork unitOfWork): IPersonService
{

    public async Task MergePeopleAsync(Person dst, Person src)
    {

        if (string.IsNullOrWhiteSpace(dst.Description) && !string.IsNullOrWhiteSpace(src.Description))
        {
            dst.Description = src.Description;
        }

        if (dst.MalId == 0 && src.MalId != 0)
        {
            dst.MalId = src.MalId;
        }

        if (dst.AniListId == 0 && src.AniListId != 0)
        {
            dst.AniListId = src.AniListId;
        }

        if (dst.HardcoverId == null && src.HardcoverId != null)
        {
            dst.HardcoverId = src.HardcoverId;
        }

        if (dst.Asin == null && src.Asin != null)
        {
            dst.Asin = src.Asin;
        }

        if (dst.CoverImage == null && src.CoverImage != null)
        {
            dst.CoverImage = src.CoverImage;
        }

        foreach (var chapter in src.ChapterPeople)
        {
            dst.ChapterPeople.Add(new ChapterPeople
            {
                Role = chapter.Role,
                Chapter = chapter.Chapter,
                Person = dst,
                KavitaPlusConnection = chapter.KavitaPlusConnection,
                OrderWeight = chapter.OrderWeight,
            });
        }

        foreach (var series in src.SeriesMetadataPeople)
        {
            dst.SeriesMetadataPeople.Add(new SeriesMetadataPeople
            {
                Role = series.Role,
                Person = dst,
                KavitaPlusConnection = series.KavitaPlusConnection,
                OrderWeight = series.OrderWeight,
            });
        }

        dst.Aliases.Add(new PersonAlias
        {
            Alias = src.Name,
            NormalizedAlias = src.NormalizedName,
        });

        unitOfWork.PersonRepository.Remove(src);
        unitOfWork.PersonRepository.Update(dst);
        await unitOfWork.CommitAsync();
    }

    public async Task<bool> UpdatePersonAliasesAsync(Person person, IList<string> aliases)
    {
        var normalizedAliases = aliases
            .Select(a => a.ToNormalized().Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Where(a => a != person.NormalizedName)
            .ToList();

        if (normalizedAliases.Count == 0)
        {
            person.Aliases = [];
            return true;
        }

        var others = await unitOfWork.PersonRepository.GetPeopleByNames(normalizedAliases);
        others = others.Where(p => p.Id != person.Id).ToList();

        if (others.Count != 0) return false;

        person.Aliases = aliases.Select(a => new PersonAlias
        {
            Alias = a.Trim(),
            NormalizedAlias = a.Trim().ToNormalized()
        }).ToList();

        return true;
    }
}

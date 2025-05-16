using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Person;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Services;

public interface IPersonService
{
    /// <summary>
    /// Adds src as an alias to dst, this is a destructive operation
    /// </summary>
    /// <param name="src">Merged person</param>
    /// <param name="dst">Remaining person</param>
    /// <remarks>The entities passed as arguments **must** include all relations</remarks>
    /// <returns></returns>
    Task MergePeopleAsync(Person src, Person dst);

    /// <summary>
    /// Adds the alias to the person, requires that the aliases are not shared with anyone else
    /// </summary>
    /// <remarks>This method does NOT commit changes</remarks>
    /// <param name="person"></param>
    /// <param name="aliases"></param>
    /// <returns></returns>
    Task<bool> UpdatePersonAliasesAsync(Person person, IList<string> aliases);
}

public class PersonService(IUnitOfWork unitOfWork): IPersonService
{

    public async Task MergePeopleAsync(Person src, Person dst)
    {
        if (dst.Id == src.Id) return;

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

        MergeChapterPeople(dst, src);
        MergeSeriesMetadataPeople(dst, src);

        dst.Aliases.Add(new PersonAliasBuilder(src.Name).Build());

        foreach (var alias in src.Aliases)
        {
            dst.Aliases.Add(alias);
        }

        unitOfWork.PersonRepository.Remove(src);
        unitOfWork.PersonRepository.Update(dst);
        await unitOfWork.CommitAsync();
    }

    private static void MergeChapterPeople(Person dst, Person src)
    {

        foreach (var chapter in src.ChapterPeople)
        {
            var alreadyPresent = dst.ChapterPeople
                .Any(x => x.ChapterId == chapter.ChapterId && x.Role == chapter.Role);

            if (alreadyPresent) continue;

            dst.ChapterPeople.Add(new ChapterPeople
            {
                Role = chapter.Role,
                ChapterId = chapter.ChapterId,
                Person = dst,
                KavitaPlusConnection = chapter.KavitaPlusConnection,
                OrderWeight = chapter.OrderWeight,
            });
        }
    }

    private static void MergeSeriesMetadataPeople(Person dst, Person src)
    {
        foreach (var series in src.SeriesMetadataPeople)
        {
            var alreadyPresent = dst.SeriesMetadataPeople
                .Any(x => x.SeriesMetadataId == series.SeriesMetadataId && x.Role == series.Role);

            if (alreadyPresent) continue;

            dst.SeriesMetadataPeople.Add(new SeriesMetadataPeople
            {
                SeriesMetadataId = series.SeriesMetadataId,
                Role = series.Role,
                Person = dst,
                KavitaPlusConnection = series.KavitaPlusConnection,
                OrderWeight = series.OrderWeight,
            });
        }
    }

    public async Task<bool> UpdatePersonAliasesAsync(Person person, IList<string> aliases)
    {
        var normalizedAliases = aliases
            .Select(a => a.ToNormalized())
            .Where(a => !string.IsNullOrEmpty(a) && a != person.NormalizedName)
            .ToList();

        if (normalizedAliases.Count == 0)
        {
            person.Aliases = [];
            return true;
        }

        var others = await unitOfWork.PersonRepository.GetPeopleByNames(normalizedAliases);
        others = others.Where(p => p.Id != person.Id).ToList();

        if (others.Count != 0) return false;

        person.Aliases = aliases.Select(a => new PersonAliasBuilder(a).Build()).ToList();

        return true;
    }
}

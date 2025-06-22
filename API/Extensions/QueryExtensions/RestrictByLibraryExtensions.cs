using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Person;

namespace API.Extensions.QueryExtensions;

// TODO: Refactor with IQueryable userLibs? But then I can't do the allLibs check?
/// <summary>
/// Optionally pass ids of all libraries, will then be smart and not restrict if the person has access to all
/// </summary>
public static class RestrictByLibraryExtensions
{

    public static IQueryable<Person> RestrictByLibrary(this IQueryable<Person> query, IQueryable<int> userLibs)
    {
        return query.Where(p =>
            p.ChapterPeople.Any(cp => userLibs.Contains(cp.Chapter.Volume.Series.LibraryId)) ||
            p.SeriesMetadataPeople.Any(sm => userLibs.Contains(sm.SeriesMetadata.Series.LibraryId)));
    }

    public static IQueryable<Chapter> RestrictByLibrary(this IQueryable<Chapter> query, IQueryable<int> userLibs)
    {
        return query.Where(cp => userLibs.Contains(cp.Volume.Series.LibraryId));
    }

    public static IQueryable<SeriesMetadataPeople> RestrictByLibrary(this IQueryable<SeriesMetadataPeople> query, IQueryable<int> userLibs)
    {
        return query.Where(sm => userLibs.Contains(sm.SeriesMetadata.Series.LibraryId));
    }

    public static IQueryable<ChapterPeople> RestrictByLibrary(this IQueryable<ChapterPeople> query, IQueryable<int> userLibs)
    {
        return query.Where(cp => userLibs.Contains(cp.Chapter.Volume.Series.LibraryId));
    }
}

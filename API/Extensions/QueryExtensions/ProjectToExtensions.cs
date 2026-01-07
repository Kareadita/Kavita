
using System.Linq;
using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace API.Extensions.QueryExtensions;

public static class ProjectToExtensions
{
    public static IQueryable<TDestination> ProjectToWithProgress<TSource, TDestination>(
        this IQueryable<TSource> queryable,
        IConfigurationProvider config,
        int userId)
    {
        return queryable.ProjectTo<TDestination>(config, new { userId });
    }

    // Convenience overload taking IMapper directly
    public static IQueryable<TDestination> ProjectToWithProgress<TSource, TDestination>(
        this IQueryable<TSource> queryable,
        IMapper mapper,
        int userId)
    {
        return queryable.ProjectTo<TDestination>(mapper.ConfigurationProvider, new { userId });
    }
}

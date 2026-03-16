using System.Linq;
using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace Kavita.Database.Extensions;

public static class ProjectToExtensions
{
    extension<TSource>(IQueryable<TSource> queryable)
    {
        public IQueryable<TDestination> ProjectToWithProgress<TDestination>(IConfigurationProvider config,
            int userId)
        {
            return queryable.ProjectTo<TDestination>(config, new { userId });
        }

        // Convenience overload taking IMapper directly
        public IQueryable<TDestination> ProjectToWithProgress<TDestination>(IMapper mapper,
            int userId)
        {
            return queryable.ProjectTo<TDestination>(mapper.ConfigurationProvider, new { userId });
        }
    }
}


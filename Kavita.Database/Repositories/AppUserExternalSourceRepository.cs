using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.SideNav;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;


public class AppUserExternalSourceRepository(DataContext context, IMapper mapper) : IAppUserExternalSourceRepository
{

    public void Update(AppUserExternalSource source)
    {
        context.AppUserExternalSource.Update(source);
    }

    public void Delete(AppUserExternalSource source)
    {
        context.AppUserExternalSource.Remove(source);
    }

    public async Task<AppUserExternalSource?> GetById(int externalSourceId, CancellationToken ct = default)
    {
        return await context.AppUserExternalSource
            .Where(s => s.Id == externalSourceId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IList<ExternalSourceDto>> GetExternalSources(int userId, CancellationToken ct = default)
    {
        return await context.AppUserExternalSource.Where(s => s.AppUserId == userId)
            .ProjectTo<ExternalSourceDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Checks if all the properties match exactly. This will allow a user to setup 2 External Sources with different Users
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="name"></param>
    /// <param name="host"></param>
    /// <param name="apiKey"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> ExternalSourceExists(int userId, string name, string host, string apiKey,
        CancellationToken ct = default)
    {
        host = host.Trim();
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(apiKey)) return false;
        var hostWithEndingSlash = UrlHelper.EnsureEndsWithSlash(host)!;
        return await context.AppUserExternalSource
            .Where(s => s.AppUserId == userId )
            .Where(s => s.Host.ToUpper().Equals(hostWithEndingSlash.ToUpper())
                        && s.Name.ToUpper().Equals(name.ToUpper())
                        && s.ApiKey.Equals(apiKey))
            .AnyAsync(ct);
    }
}

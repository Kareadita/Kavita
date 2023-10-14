using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.SideNav;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IAppUserExternalSourceRepository
{
    void Update(AppUserExternalSource source);
    void Delete(AppUserExternalSource source);
    Task<AppUserExternalSource> GetById(int externalSourceId);
    Task<IList<ExternalSourceDto>> GetExternalSources(int userId);
    Task<bool> ExternalSourceExists(int userId, string name, string host, string apiKey);
}

public class AppUserExternalSourceRepository : IAppUserExternalSourceRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public AppUserExternalSourceRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(AppUserExternalSource source)
    {
        _context.Entry(source).State = EntityState.Modified;
    }

    public void Delete(AppUserExternalSource source)
    {
        _context.AppUserExternalSource.Remove(source);
    }

    public async Task<AppUserExternalSource> GetById(int externalSourceId)
    {
        return await _context.AppUserExternalSource
            .Where(s => s.Id == externalSourceId)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<ExternalSourceDto>> GetExternalSources(int userId)
    {
        return await _context.AppUserExternalSource.Where(s => s.AppUserId == userId)
            .ProjectTo<ExternalSourceDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    /// <summary>
    /// Checks if all the properties match exactly. This will allow a user to setup 2 External Sources with different Users
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="host"></param>
    /// <param name="name"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    public async Task<bool> ExternalSourceExists(int userId, string name, string host, string apiKey)
    {
        host = host.Trim();
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(apiKey)) return false;
        var hostWithEndingSlash = UrlHelper.EnsureEndsWithSlash(host)!;
        return await _context.AppUserExternalSource
            .Where(s => s.AppUserId == userId )
            .Where(s => s.Host.ToUpper().Equals(hostWithEndingSlash.ToUpper())
                        && s.Name.ToUpper().Equals(name.ToUpper())
                        && s.ApiKey.Equals(apiKey))
            .AnyAsync();
    }
}

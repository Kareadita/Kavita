using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.SideNav;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IAppUserExternalSourceRepository
{
    void Update(AppUserExternalSource source);
    void Delete(AppUserExternalSource source);
    Task<AppUserExternalSource> GetById(int externalSourceId);
    Task<IList<ExternalSourceDto>> GetExternalSources(int userId);
    Task<bool> ExternalSourceExists(int userId, string host, string name);
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

    public async Task<bool> ExternalSourceExists(int userId, string host, string name)
    {
        host = host.Trim();
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(name)) return false;
        return await _context.AppUserExternalSource
            .Where(s => s.AppUserId == userId )
            .Where(s => EF.Functions.Like(s.Host, $"%{host}%")
                        || s.Name.ToUpper().Equals(name.ToUpper()))
            .AnyAsync();
    }
}

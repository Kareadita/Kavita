using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Dashboard;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IAppUserSmartFilterRepository
{
    void Update(AppUserSmartFilter filter);
    void Attach(AppUserSmartFilter filter);
    void Delete(AppUserSmartFilter filter);
    IEnumerable<SmartFilterDto> GetAllDtosByUserId(int userId);
    Task<AppUserSmartFilter?> GetById(int smartFilterId);

}

public class AppUserSmartFilterRepository : IAppUserSmartFilterRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public AppUserSmartFilterRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(AppUserSmartFilter filter)
    {
        _context.Entry(filter).State = EntityState.Modified;
    }

    public void Attach(AppUserSmartFilter filter)
    {
        _context.AppUserSmartFilter.Attach(filter);
    }

    public void Delete(AppUserSmartFilter filter)
    {
        _context.AppUserSmartFilter.Remove(filter);
    }

    public IEnumerable<SmartFilterDto> GetAllDtosByUserId(int userId)
    {
        return _context.AppUserSmartFilter
            .Where(f => f.AppUserId == userId)
            .ProjectTo<SmartFilterDto>(_mapper.ConfigurationProvider)
            .AsEnumerable();
    }

    public async Task<AppUserSmartFilter?> GetById(int smartFilterId)
    {
        return await _context.AppUserSmartFilter
            .FirstOrDefaultAsync(d => d.Id == smartFilterId);
    }
}

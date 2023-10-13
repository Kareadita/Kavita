using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IAppUserExternalSourceRepository
{
    void Update(AppUserExternalSource source);
    void Delete(AppUserExternalSource source);
    Task<AppUserExternalSource> GetById(int externalSourceId);
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
}

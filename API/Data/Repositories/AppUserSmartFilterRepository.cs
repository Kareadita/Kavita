using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IAppUserSmartFilterRepository
{
    void Update(AppUserSmartFilter filter);
    void Attach(AppUserSmartFilter filter);
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
}

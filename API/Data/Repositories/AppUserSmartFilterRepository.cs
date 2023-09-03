using AutoMapper;

namespace API.Data.Repositories;

public interface IAppUserSmartFilterRepository
{

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
}

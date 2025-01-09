using System.Collections.Generic;
using System.Threading.Tasks;
using API.Entities;
using AutoMapper;

namespace API.Data.Repositories;

public interface IEmailHistoryRepository
{

}

public class EmailHistoryRepository : IEmailHistoryRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public EmailHistoryRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }



}

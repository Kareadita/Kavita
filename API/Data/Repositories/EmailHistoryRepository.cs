using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Email;
using API.Entities;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IEmailHistoryRepository
{
    Task<IList<EmailHistoryDto>> GetEmailDtos(UserParams userParams);
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


    public async Task<IList<EmailHistoryDto>> GetEmailDtos(UserParams userParams)
    {
        return await _context.EmailHistory
            .OrderByDescending(h => h.SendDate)
            .ProjectTo<EmailHistoryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}

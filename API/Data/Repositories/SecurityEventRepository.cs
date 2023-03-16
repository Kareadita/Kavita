using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface ISecurityEventRepository
{
    void Add(SecurityEvent securityEvent);
}

public class SecurityEventRepository : ISecurityEventRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public SecurityEventRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Add(SecurityEvent securityEvent)
    {
        _context.SecurityEvent.Add(securityEvent);
    }
}

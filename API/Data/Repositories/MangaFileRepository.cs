using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IMangaFileRepository
{
    void Update(MangaFile file);
}

public class MangaFileRepository : IMangaFileRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public MangaFileRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(MangaFile file)
    {
        _context.Entry(file).State = EntityState.Modified;
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IMangaFileRepository
{
    void Update(MangaFile file);
    Task<bool> AnyMissingExtension();
    Task<IList<MangaFile>> GetAllWithMissingExtension();
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

    public async Task<bool> AnyMissingExtension()
    {
        return (await _context.MangaFile.CountAsync(f => string.IsNullOrEmpty(f.Extension))) > 0;
    }

    public async Task<IList<MangaFile>> GetAllWithMissingExtension()
    {
        return await _context.MangaFile
            .Where(f => string.IsNullOrEmpty(f.Extension))
            .ToListAsync();
    }
}

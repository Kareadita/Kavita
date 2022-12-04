using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IMangaFileRepository
{
    void Update(MangaFile file);
}

public class MangaFileRepository : IMangaFileRepository
{
    private readonly DataContext _context;

    public MangaFileRepository(DataContext context)
    {
        _context = context;
    }

    public void Update(MangaFile file)
    {
        _context.Entry(file).State = EntityState.Modified;
    }
}

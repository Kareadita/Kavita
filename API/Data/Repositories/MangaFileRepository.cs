using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IMangaFileRepository
{
    void Update(MangaFile file);
    Task<IList<MangaFile>> GetAllWithMissingExtension();
    Task<MangaFile?> GetByKoreaderHash(string hash);
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

    public async Task<IList<MangaFile>> GetAllWithMissingExtension()
    {
        return await _context.MangaFile
            .Where(f => string.IsNullOrEmpty(f.Extension))
            .ToListAsync();
    }

    public async Task<MangaFile?> GetByKoreaderHash(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return null;

        return await _context.MangaFile
            .FirstOrDefaultAsync(f => f.KoreaderHash != null &&
                                    f.KoreaderHash.Equals(hash.ToUpper()));
    }
}

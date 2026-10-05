using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class MangaFileRepository(DataContext context) : IMangaFileRepository
{
    public void Update(MangaFile file)
    {
        context.Entry(file).State = EntityState.Modified;
    }

    public async Task<IList<MangaFile>> GetAllWithMissingExtension(CancellationToken ct = default)
    {
        return await context.MangaFile
            .Where(f => string.IsNullOrEmpty(f.Extension))
            .ToListAsync(ct);
    }

    public async Task<MangaFile?> GetByKoreaderHash(string hash, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(hash)) return null;

        return await context.MangaFile
            .FirstOrDefaultAsync(f => f.KoreaderHash != null &&
                                    f.KoreaderHash.Equals(hash.ToUpper()), ct);
    }

    /// <summary>
    /// Writes the file's own write time without loading the rows. Bypasses the save interceptor on purpose: on MangaFile,
    /// LastModified is the write time cover and page checks last ran against, and stamping it with now would hide a pending regen
    /// </summary>
    public async Task SetFileLastWriteTimesAsync(IReadOnlyDictionary<int, DateTime> writeTimesByFileId, CancellationToken ct = default)
    {
        if (writeTimesByFileId.Count == 0) return;

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        foreach (var (id, writeTime) in writeTimesByFileId)
        {
            await context.MangaFile
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.FileLastWriteTimeUtc, writeTime), ct);
        }
        await tx.CommitAsync(ct);
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Models.Entities.History;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class KavitaPlusAuditRepository(DataContext context) : IKavitaPlusAuditRepository
{
    public void Add(KavitaPlusAuditLog entry) => context.KavitaPlusAuditLogs.Add(entry);

    public async Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        await context.KavitaPlusAuditLogs
            .Where(e => e.CreatedUtc < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}

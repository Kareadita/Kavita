using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.History;

namespace Kavita.API.Repositories;

public interface IKavitaPlusAuditRepository
{
    void Add(KavitaPlusAuditLog entry);
    Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}

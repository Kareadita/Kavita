using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Progress;

namespace Kavita.API.Repositories;

public interface IReadingSessionRepository
{
    Task<IList<ReadingSessionDto>> GetAllReadingSessionAsync(bool isActiveOnly = true, CancellationToken ct = default);
}

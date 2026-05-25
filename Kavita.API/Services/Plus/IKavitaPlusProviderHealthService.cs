using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus;

namespace Kavita.API.Services.Plus;

public interface IKavitaPlusProviderHealthService
{
    Task<IList<KavitaPlusProviderHealthSnapshotDto>> GetProviderHealthSnapshot(bool forceCheck = false, CancellationToken ct = default);
}

using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services.Kobo;

public interface IKoboOptimizationService
{
    Task<string> GetKoboDownloadPathAsync(int userId, int chapterId, bool convert, string? koboDeviceId, CancellationToken ct = default);
}

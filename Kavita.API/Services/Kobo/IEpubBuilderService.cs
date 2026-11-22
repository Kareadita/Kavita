using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services.Kobo;

public interface IEpubBuilderService
{
    Task<string> BuildEpubAsync(int userId, int chapterId, string outputPath, CancellationToken ct = default);
}

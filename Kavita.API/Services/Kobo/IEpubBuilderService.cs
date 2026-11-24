using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Images;

namespace Kavita.API.Services.Kobo;

public interface IEpubBuilderService
{
    Task<string> BuildEpubAsync(BuildEpubOptions options, int userId, LocalizedNamingContext naming, CancellationToken ct = default);
}

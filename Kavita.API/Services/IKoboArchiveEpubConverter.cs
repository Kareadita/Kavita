using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

/// <summary>
/// Builds an EPUB from a CBZ/CBR (or other comic archive) on disk.
/// </summary>
public interface IKoboArchiveEpubConverter
{
    Task ConvertAsync(string archivePath, string outputEpubPath, string title, CancellationToken ct = default);
}

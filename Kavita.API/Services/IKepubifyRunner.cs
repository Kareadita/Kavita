using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

/// <summary>
/// Runs the kepubify binary to convert an EPUB into a Kobo KEPUB on disk.
/// </summary>
public interface IKepubifyRunner
{
    /// <summary>
    /// Converts <paramref name="inputEpubPath"/> to <paramref name="outputKepubPath"/> using
    /// <paramref name="kepubifyBinaryPath"/>.
    /// </summary>
    Task ConvertAsync(string kepubifyBinaryPath, string inputEpubPath, string outputKepubPath,
        CancellationToken ct = default);
}

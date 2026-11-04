using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

/// <summary>
/// This is a singleton
/// </summary>
public interface IKepubifyConverterService
{
    bool KepubifySupported { get; }

    string BuildKepubifyPath(int chapterId);

    Task CheckKepubifySupport(string kepubifyPath, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the book's conversion, starting it if none is running. Cancelling only stops this caller's wait
    /// </summary>
    /// <returns>True when the file is at <paramref name="kepubPath"/>. False when the conversion is not supported, failed, timed out, or the server is stopping</returns>
    /// <exception cref="System.OperationCanceledException"><paramref name="cancellationToken"/> was cancelled</exception>
    Task<bool> ConvertEpubToKepub(string epubPath, string kepubPath, CancellationToken cancellationToken);
}

using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Settings;

namespace Kavita.API.Services;

/// <summary>
/// This is a singleton
/// </summary>
public interface IKepubifyConverterService
{
    KepubifyStatusDto Status { get; }

    bool KepubifySupported { get; }

    string BuildKepubifyPath(int chapterId);

    /// <summary>
    /// Sets the status to Checking, so kepub is off until the queued check sends its result to admins
    /// </summary>
    KepubifyStatusDto QueueCheck();

    /// <summary>
    /// Picks the binary (KepubifyPath in appsettings.json, else the shipped one), checks it and sends the result to admins.
    /// A check started while one is running waits for it
    /// </summary>
    Task<KepubifyStatusDto> CheckKepubifySupport(CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the book's conversion, starting it if none is running. Cancelling only stops this caller's wait
    /// </summary>
    /// <returns>True when the file is at <paramref name="kepubPath"/>. False when the conversion is not supported, failed, timed out, or the server is stopping</returns>
    /// <exception cref="System.OperationCanceledException"><paramref name="cancellationToken"/> was cancelled</exception>
    Task<bool> ConvertEpubToKepub(string epubPath, string kepubPath, CancellationToken cancellationToken);
}

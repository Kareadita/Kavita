namespace Kavita.API.Services;

/// <summary>
/// Enqueues background Kobo archive conversion (Hangfire in production).
/// </summary>
public interface IKoboConversionJobScheduler
{
    void EnqueueBackgroundConvert(int chapterId);

    /// <summary>
    /// Enqueues in-place promotion of a cached KEPUB into the library folder (replaces the original EPUB).
    /// </summary>
    void EnqueuePromoteKepubToLibrary(int chapterId);
}

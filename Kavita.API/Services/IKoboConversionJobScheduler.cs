namespace Kavita.API.Services;

/// <summary>
/// Enqueues background Kobo archive conversion (Hangfire in production).
/// </summary>
public interface IKoboConversionJobScheduler
{
    void EnqueueBackgroundConvert(int chapterId);
}

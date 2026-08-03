using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

/// <summary>
/// Rematches Kobo Location against a new device-openable artifact (e.g. KEPUB first-available).
/// </summary>
public interface IKoboLocationRematchService
{
    /// <summary>
    /// For each user progress/Location on <paramref name="chapterId"/>, rematch against
    /// <paramref name="newDeviceOpenablePath"/>. Convert chapters re-encode from
    /// <c>PagesRead</c> when the new file is a trusted KEPUB; prose chapters remap from
    /// <c>BookScrollId</c> or keep Location only when still valid-in-file. Never clears
    /// <c>BookScrollId</c> or percent/<c>PagesRead</c>.
    /// </summary>
    Task RematchAfterDeviceFileChangeAsync(int chapterId, string newDeviceOpenablePath,
        CancellationToken ct = default);
}

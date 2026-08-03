using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

/// <summary>
/// Rematches Kobo Location against a new device-openable artifact (e.g. KEPUB first-available).
/// </summary>
public interface IKoboLocationRematchService
{
    /// <summary>
    /// For each user progress/Location on <paramref name="chapterId"/>, remap from
    /// <c>BookScrollId</c> against <paramref name="newDeviceOpenablePath"/>. Keep Location only
    /// when valid in that file; otherwise clear Location columns. Never clears
    /// <c>BookScrollId</c> or percent/<c>PagesRead</c>.
    /// </summary>
    Task RematchAfterDeviceFileChangeAsync(int chapterId, string newDeviceOpenablePath,
        CancellationToken ct = default);
}

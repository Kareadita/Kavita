namespace Kavita.Models.DTOs.Settings;
#nullable enable

/// <param name="Path">The full path that was checked. Empty while the first check is running</param>
/// <param name="Reason">Set only when <paramref name="Status"/> is NotSupported</param>
public sealed record KepubifyStatusDto(KepubifyCheckStatus Status, KepubifySource Source, string Path, KepubifyOffReason? Reason)
{
    public static readonly KepubifyStatusDto Checking = new(KepubifyCheckStatus.Checking, KepubifySource.Shipped, string.Empty, null);
}

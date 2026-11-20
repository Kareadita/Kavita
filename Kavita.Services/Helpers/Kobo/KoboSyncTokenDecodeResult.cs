using Kavita.Models.DTOs.Kobo;

namespace Kavita.Services.Helpers.Kobo;

public sealed record KoboSyncTokenDecodeResult
{
    public KoboSyncToken Token { get; init; } = new();

    public string? KoboStoreSyncToken { get; init; }
}

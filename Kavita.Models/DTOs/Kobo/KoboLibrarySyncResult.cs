using System.Text.Json.Nodes;

namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// Library sync page: entitlement array plus response headers.
/// </summary>
public class KoboLibrarySyncResult
{
    public required JsonArray Items { get; init; }
    public required string SyncToken { get; init; }
    public bool Continue { get; init; }
}

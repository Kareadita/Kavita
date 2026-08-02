namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// Native EPUB download resolved for a Kobo entitlement.
/// </summary>
public class KoboDownloadResult
{
    public required string FilePath { get; init; }
    public required string ContentType { get; init; }
    public required string FileDownloadName { get; init; }
}

#nullable enable
namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboBookEntitlementContainerDto
{
    public required KoboBookEntitlementDto BookEntitlement { get; set; }
    public required KoboBookMetadataDto BookMetadata { get; set; }
    public required KoboReadingStateDto? ReadingState { get; set; }
}

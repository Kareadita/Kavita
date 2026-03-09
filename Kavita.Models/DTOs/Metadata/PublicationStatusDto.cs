using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Metadata;

public sealed record PublicationStatusDto
{
    public PublicationStatus Value { get; set; }
    public required string Title { get; set; }
}

using API.Entities.Enums;

namespace API.DTOs.Metadata;

public sealed record PublicationStatusDto
{
    public PublicationStatus Value { get; set; }
    public required string Title { get; set; }
}

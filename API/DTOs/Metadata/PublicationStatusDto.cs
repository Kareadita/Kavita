using API.Entities.Enums;

namespace API.DTOs.Metadata;

public class PublicationStatusDto
{
    public PublicationStatus Value { get; set; }
    public required string Title { get; set; }
}

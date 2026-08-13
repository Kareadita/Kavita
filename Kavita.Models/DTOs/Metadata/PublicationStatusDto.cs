using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Metadata;

public sealed record PublicationStatusDto
{
    [EnumDataType(typeof(PublicationStatus))]
    public PublicationStatus Value { get; set; }
    public required string Title { get; set; }
}
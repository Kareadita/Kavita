using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

public sealed record MetadataFieldMappingDto
{
    public int Id { get; set; }
    [EnumDataType(typeof(MetadataFieldType))]
    public MetadataFieldType SourceType { get; set; }
    [EnumDataType(typeof(MetadataFieldType))]
    public MetadataFieldType DestinationType { get; set; }
    /// <summary>
    /// The string in the source
    /// </summary>
    public string SourceValue { get; set; }
    /// <summary>
    /// Write the string as this in the Destination (can also just be the Source)
    /// </summary>
    public string DestinationValue { get; set; }
    /// <summary>
    /// If true, the tag will be Moved over vs Copied over
    /// </summary>
    public bool ExcludeFromSource { get; set; }
}
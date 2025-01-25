using API.Entities.Enums;

namespace API.Entities;

public class MetadataFieldMapping
{
    public int Id { get; set; }
    public MetadataFieldType SourceType { get; set; }
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

    public int MetadataSettingsId { get; set; }
    public virtual MetadataSettings MetadataSettings { get; set; }
}

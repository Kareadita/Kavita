using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Metadata.Matching;

/// <summary>
/// Used to change (or clear) a Series' <see cref="Kavita.Models.Entities.Series.MetadataProviderOverride"/> from the Match Series dialog
/// </summary>
public sealed record UpdateSeriesMetadataProviderOverrideDto
{
    public int SeriesId { get; set; }
    /// <summary>
    /// Null clears the override and falls back to the Library's default Metadata Provider
    /// </summary>
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider? MetadataProviderOverride { get; set; }
}

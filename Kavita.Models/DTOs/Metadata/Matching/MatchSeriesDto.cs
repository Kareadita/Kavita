using System.ComponentModel.DataAnnotations;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Metadata.Matching;

/// <summary>
/// Used for matching a series with Kavita+ for metadata and scrobbling
/// </summary>
public sealed record MatchSeriesDto
{
    /// <summary>
    /// Series Id to pull internal metadata from to improve matching
    /// </summary>
    public int SeriesId { get; set; }
    /// <summary>
    /// Free form text to query for. Can be a url and ids will be parsed from it
    /// </summary>
    public string Query { get; set; }
    /// <summary>
    /// If the series should be consider a book (Hardcover)
    /// </summary>
    public bool IsStandAlone { get; set; }
    /// <summary>
    /// Search against this Provider instead of the Series' current one. Nothing is written to the Series, the
    /// Provider is only applied to this search
    /// </summary>
    /// <remarks>A provider-specific url/header in <see cref="Query"/> takes precedence over this</remarks>
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider? Provider { get; set; }
}

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Metadata.Matching;

/// <summary>
/// The results of a match search, along with the Provider they came from
/// </summary>
public sealed record MatchSeriesResultDto
{
    /// <summary>
    /// The Provider the search was actually performed against. Can differ from the Series' current Provider when a
    /// provider-specific url/header was queried, or another Provider was explicitly requested
    /// </summary>
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider Provider { get; set; }
    public IList<ExternalSeriesMatchDto> Matches { get; set; } = [];
}

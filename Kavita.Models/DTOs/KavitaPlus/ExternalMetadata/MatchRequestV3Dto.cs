using System.Collections.Generic;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
#nullable enable

public sealed record MatchRequestV3Dto: MetadataRequest
{
    [EnumDataType(typeof(MetadataProvider))]
    public required MetadataProvider Provider { get; set; }
    public required string SeriesName { get; set; }
    public List<string> AlternativeNames { get; set; } = [];
    public int? Year { get; set; }
    public string? Query { get; set; }
    [EnumDataType(typeof(PlusMediaFormat))]
    public PlusMediaFormat Format { get; set; }
}
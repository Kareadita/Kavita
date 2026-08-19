using Kavita.Models.DTOs.Common;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs;
#nullable enable

public sealed record UpdateSeriesDto : IUpdateExternalMetadataIds
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? LocalizedName { get; init; }
    public string? SortName { get; init; }
    public bool CoverImageLocked { get; set; }

    public bool NameLocked { get; set; }
    public bool SortNameLocked { get; set; }
    public bool LocalizedNameLocked { get; set; }

    /// <summary>
    /// Overrides the parent Library's default Metadata Provider for this Series only.
    /// Null means the Series should inherit the Library's default provider.
    /// </summary>
    /// <inheritdoc cref="Kavita.Models.Entities.Series.MetadataProviderOverride"/>
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider? MetadataProviderOverride { get; set; }

    #region External Metadata Ids
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? HardcoverId { get; set; }
    public long? MetronId { get; set; }
    public string? ComicVineId { get; set; }
    public int? MangaBakaId { get; set; }
    public int? CbrId { get; set; }
    #endregion
}

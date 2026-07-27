#nullable enable
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;



public sealed record SeriesRelationship
{
    public int AniListId { get; set; }
    public int? MalId { get; set; }
    /// <summary>
    /// MangaBaka series id. Often the only navigable id for MangaBaka-sourced relationships.
    /// </summary>
    public int? MangabakaId { get; set; }
    public ALMediaTitle SeriesName { get; set; }
    public RelationKind Relation { get; set; }
    public ScrobbleProvider Provider { get; set; }
    public PlusMediaFormat Format { get; set; } = PlusMediaFormat.Manga;
    public ExternalSeriesDetailDto Series { get; set; }
    public MetadataProvider MetadataProvider { get; set; }
}

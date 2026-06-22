#nullable enable
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

public sealed record ALMediaTitle
{
    public string? EnglishTitle { get; set; }
    public string RomajiTitle { get; set; }
    public string NativeTitle { get; set; }
    public string PreferredTitle { get; set; }
}

public sealed record SeriesRelationship
{
    public int AniListId { get; set; }
    public int? MalId { get; set; }
    public ALMediaTitle SeriesName { get; set; }
    public RelationKind Relation { get; set; }
    public ScrobbleProvider Provider { get; set; }
    public PlusMediaFormat PlusMediaFormat { get; set; } = PlusMediaFormat.Manga;
}

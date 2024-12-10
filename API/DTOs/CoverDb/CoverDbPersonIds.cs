using YamlDotNet.Serialization;

namespace API.DTOs.CoverDb;
#nullable enable

public class CoverDbPersonIds
{
    [YamlMember(Alias = "hardcover_id", ApplyNamingConventions = false)]
    public string? HardcoverId { get; set; } = null;
    [YamlMember(Alias = "amazon_id", ApplyNamingConventions = false)]
    public string? AmazonId { get; set; } = null;
    [YamlMember(Alias = "metron_id", ApplyNamingConventions = false)]
    public string? MetronId { get; set; } = null;
    [YamlMember(Alias = "comicvine_id", ApplyNamingConventions = false)]
    public string? ComicVineId { get; set; } = null;
    [YamlMember(Alias = "anilist_id", ApplyNamingConventions = false)]
    public string? AnilistId { get; set; } = null;
    [YamlMember(Alias = "mal_id", ApplyNamingConventions = false)]
    public string? MALId { get; set; } = null;
}

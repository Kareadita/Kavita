namespace Kavita.Models.DTOs.KavitaPlus.Audit;
#nullable enable

public sealed record AuditLogPersonAliasParamsDto
{
    public string PersonName { get; init; } = string.Empty;
    public string AliasAdded { get; init; } = string.Empty;
}

public sealed record AuditLogPersonCoverParamsDto
{
    public string PersonName { get; init; } = string.Empty;
    public int AniListId { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
}

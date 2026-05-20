namespace Kavita.Models.DTOs.KavitaPlus.Audit;
#nullable enable

public sealed record AuditLogCollectionItemParamsDto
{
    public string CollectionName { get; init; } = string.Empty;
    public string SeriesName { get; init; } = string.Empty;
    public int SeriesId { get; init; }
}

public sealed record AuditLogCollectionSyncedParamsDto
{
    public string CollectionName { get; init; } = string.Empty;
    public string? StackId { get; init; }
    public int ItemCount { get; init; }
    public int MissingCount { get; init; }
}

public sealed record AuditLogCollectionFailedParamsDto
{
    public string CollectionName { get; init; } = string.Empty;
}

public sealed record AuditLogCollectionStartedParamsDto
{
    public string CollectionName { get; init; } = string.Empty;
    public string? StackId { get; init; }
    public int TotalItems { get; init; }
}

public sealed record AuditLogWantToReadSyncParamsDto
{
    public string UserName { get; init; } = string.Empty;
    public bool HasMal { get; init; }
    public bool HasAniList { get; init; }
}

public sealed record AuditLogWantToReadSyncCompletedParamsDto
{
    public string UserName { get; init; } = string.Empty;
    public int SeriesMatched { get; init; }
    public bool HasMal { get; init; }
    public bool HasAniList { get; init; }
}

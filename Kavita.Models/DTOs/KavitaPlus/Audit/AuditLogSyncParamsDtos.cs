using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus.Audit;
#nullable enable

public sealed record AuditLogCollectionItemParamsDto
{
    public string CollectionName { get; init; } = string.Empty;
    public string SeriesName { get; init; } = string.Empty;
    public int SeriesId { get; init; }
    public string Url { get; init; } = string.Empty;
}

public sealed record AuditLogCollectionSyncedParamsDto
{
    public string CollectionName { get; init; } = string.Empty;
    public string? StackId { get; init; }
    public int ItemCount { get; init; }
    public int MissingCount { get; init; }
    public string Url { get; init; } = string.Empty;
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
    [Obsolete("Use Providers instead")]
    public bool HasMal { get; init; }
    [Obsolete("Use Providers instead")]
    public bool HasAniList { get; init; }
    public List<ScrobbleProvider> Providers { get; init; }
}

public sealed record AuditLogWantToReadSyncCompletedParamsDto
{
    public string UserName { get; init; } = string.Empty;
    public int SeriesMatched { get; init; }
    [Obsolete("Use Providers instead")]
    public bool HasMal { get; init; }
    [Obsolete("Use Providers instead")]
    public bool HasAniList { get; init; }
    public List<ScrobbleProvider> Providers { get; init; }
}

#nullable enable
using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboSyncItem
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KoboBookEntitlementContainerDto? NewEntitlement { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KoboBookEntitlementContainerDto? ChangedEntitlement { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KoboBookMetadataDto? ChangedProductMetadata { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KoboWrappedReadingStateDto? ChangedReadingState { get; init; }
}

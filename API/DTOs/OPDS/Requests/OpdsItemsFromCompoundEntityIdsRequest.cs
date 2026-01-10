using API.Entities.Enums.UserPreferences;

namespace API.DTOs.OPDS.Requests;

/// <summary>
/// A special case for dealing with lower level entities (volume/chapter) which need higher level entity ids
/// </summary>
/// <remarks>Not all variables will always be used. Implementation will use</remarks>
public sealed record OpdsItemsFromCompoundEntityIdsRequest : IOpdsRequest, IOpdsPagination
{
    public string ApiKey { get; init; }
    public string Prefix { get; init; }
    public string BaseUrl { get; init; }
    public int UserId { get; init; }
    public AppUserOpdsPreferences Preferences { get; init; }
    public int PageNumber { get; init; }

    public int SeriesId { get; init; }
    public int VolumeId { get; init; }
    public int ChapterId { get; init; }
}

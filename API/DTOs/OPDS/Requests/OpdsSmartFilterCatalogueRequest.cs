using API.Entities.Enums.UserPreferences;

namespace API.DTOs.OPDS.Requests;

/// <summary>
/// A generic Catalogue request for a specific Entity
/// </summary>
public sealed record OpdsPaginatedCatalogueRequest : IOpdsRequest, IOpdsPagination
{
    public string ApiKey { get; init; }
    public string Prefix { get; init; }
    public string BaseUrl { get; init; }
    public int UserId { get; init; }
    public AppUserOpdsPreferences Preferences { get; init; }

    public int PageNumber { get; init; }
}

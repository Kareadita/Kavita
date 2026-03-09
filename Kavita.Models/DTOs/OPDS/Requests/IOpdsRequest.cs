using Kavita.Models.Entities.Enums.UserPreferences;

namespace Kavita.Models.DTOs.OPDS.Requests;

public interface IOpdsRequest
{
    public string ApiKey { get; init; }
    public string Prefix { get; init; }
    public string BaseUrl { get; init; }
    public int UserId { get; init; }
    public AppUserOpdsPreferences Preferences { get; init; }
}

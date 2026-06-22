using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus.Scrobble;

public class UpdateScrobbleProviderDto
{
    public required ScrobbleProvider Provider { get; set; }
    public string UserName { get; set; }
    public string AuthenticationToken { get; set; }
    public string RefreshToken { get; set; }
}

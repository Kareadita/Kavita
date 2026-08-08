using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.Scrobble;

public class UpdateScrobbleProviderDto
{
    [EnumDataType(typeof(ScrobbleProvider))]
    public required ScrobbleProvider Provider { get; set; }
    public string UserName { get; set; }
    public string AuthenticationToken { get; set; }
    public string RefreshToken { get; set; }
}
using System.ComponentModel.DataAnnotations;
namespace Kavita.Models.DTOs.KavitaPlus.OAuth;

public sealed record RefreshTokenRequestDto
{
    [EnumDataType(typeof(OAuthUpstream))]
    public required OAuthUpstream Upstream { get; set; }
    public required string RefreshToken { get; set; }
}
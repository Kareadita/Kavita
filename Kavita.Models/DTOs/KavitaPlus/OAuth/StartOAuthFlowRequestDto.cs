using Microsoft.AspNetCore.DataProtection;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.OAuth;

public sealed record StartOAuthFlowRequestDto
{
    [EnumDataType(typeof(OAuthUpstream))]
    public required OAuthUpstream Upstream { get; set; }
    public required string InstanceUrl { get; set; }
    /// <summary>
    /// The ApiKey should be encrypted by calling <see cref="IDataProtector.Protect"/>
    /// </summary>
    public required string ApiKey { get; set; }
}
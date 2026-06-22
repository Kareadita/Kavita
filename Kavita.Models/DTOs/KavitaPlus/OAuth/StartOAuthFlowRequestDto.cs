using Microsoft.AspNetCore.DataProtection;

namespace Kavita.Models.DTOs.KavitaPlus.OAuth;

public sealed record StartOAuthFlowRequestDto
{
    public required OAuthUpstream Upstream { get; set; }
    public required string InstanceUrl { get; set; }
    /// <summary>
    /// The ApiKey should be encrypted by calling <see cref="IDataProtector.Protect"/>
    /// </summary>
    public required string ApiKey { get; set; }
}

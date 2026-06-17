namespace Kavita.Models.DTOs.KavitaPlus.OAuth;

public sealed record StartOAuthFlowRequestDto
{
    public required OAuthUpstream Upstream { get; set; }
    public required string InstanceUrl { get; set; }
    public required string ApiKey { get; set; }
}

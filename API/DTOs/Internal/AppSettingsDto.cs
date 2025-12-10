namespace API.DTOs.Internal;
#nullable enable

public sealed record AppSettingsDto
{
    public required string TokenKey { get; set; }
    public int Port { get; set; }
    public string? IpAddresses { get; set; }
    public required string BaseUrl { get; set; }
    public int Cache { get; set; }
}

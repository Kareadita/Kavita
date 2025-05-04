namespace API.DTOs.Account;
#nullable enable

public sealed record LoginDto
{
    public string Username { get; init; } = default!;
    public string Password { get; set; } = default!;
    /// <summary>
    /// If ApiKey is passed, will ignore username/password for validation
    /// </summary>
    public string? ApiKey { get; set; } = default!;
}

namespace Kavita.Models.DTOs.Account;

public sealed record UpdateUsernameRequestDto
{
    public required string Username { get; set; }
}


using System;

namespace Kavita.Models.DTOs.Account;

public sealed record MemberInfoDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? CoverImage { get; set; }
}

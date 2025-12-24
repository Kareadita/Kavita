using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;
#nullable enable

public sealed record RotateAuthKeyRequestDto
{
    [Required]
    [Range(8, 32)]
    public int KeyLength { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
    public string? ExpiresUtc { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public sealed record PersonAliasCheckDto
{
    /// <summary>
    /// The person to check against
    /// </summary>
    [Required]
    public int PersonId { get; set; }
    /// <summary>
    /// The persons name in the form. In case it differs from the one in the database
    /// </summary>
    [Required]
    public string Name { get; set; }
    /// <summary>
    /// The alias to check
    /// </summary>
    [Required]
    public string Alias { get; set; }
}

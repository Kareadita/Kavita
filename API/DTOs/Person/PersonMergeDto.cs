using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public sealed record PersonMergeDto
{
    /// <summary>
    /// The id of the person being merged into
    /// </summary>
    [Required]
    public int DestId { get; init; }
    /// <summary>
    /// The id of the person being merged. This person will be removed, and become an alias of <see cref="DestId"/>
    /// </summary>
    [Required]
    public int SrcId { get; init; }
}

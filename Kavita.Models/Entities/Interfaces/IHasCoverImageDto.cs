namespace Kavita.Models.Entities.Interfaces;

/// <summary>
/// Special interface to aid to AutoMapper
/// </summary>
public interface IHasCoverImageDto
{
    /// <summary>
    /// Is a CoverImage set for Entity
    /// </summary>
    public bool HasCoverImage { get; set; }
}

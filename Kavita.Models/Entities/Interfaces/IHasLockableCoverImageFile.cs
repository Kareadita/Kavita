namespace Kavita.Models.Entities.Interfaces;

/// <summary>
/// Represents an Entity having a Lock for a Cover Image
/// </summary>
public interface IHasLockableCoverImageFile
{
    public bool CoverImageLocked { get; set; }
}

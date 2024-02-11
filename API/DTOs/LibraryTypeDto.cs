using API.Entities.Enums;

namespace API.DTOs;

/// <summary>
/// Simple pairing of LibraryId and LibraryType
/// </summary>
public class LibraryTypeDto
{
    public int LibraryId { get; set; }
    public LibraryType LibraryType { get; set; }
}

using Microsoft.EntityFrameworkCore;

namespace API.Entities;

[Index(nameof(LibraryId), nameof(AppUserId), IsUnique = true)]
public class LibraryReadingProfile
{

    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }

    public int LibraryId { get; set; }
    public Library Library { get; set; }

    public int ReadingProfileId { get; set; }
    public AppUserReadingProfile ReadingProfile { get; set; }

}

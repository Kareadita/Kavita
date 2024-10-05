using API.Entities.Enums;

namespace API.Entities;

public class ChapterPeople
{
    public int ChapterId { get; set; }
    public virtual Chapter Chapter { get; set; }

    public int PersonId { get; set; }
    public virtual Person Person { get; set; }

    public required PersonRole Role { get; set; }
}

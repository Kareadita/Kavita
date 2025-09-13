using API.Entities.Enums;

namespace API.Entities.Person;

public class ChapterPeople
{
    public int ChapterId { get; set; }
    public virtual Chapter Chapter { get; set; }

    public int PersonId { get; set; }
    public virtual Person Person { get; set; }

    /// <summary>
    /// The source of this connection. If not Kavita, this implies Metadata Download linked this and it can be removed between matches
    /// </summary>
    public bool KavitaPlusConnection { get; set; }
    /// <summary>
    /// A weight that allows lower numbers to sort first
    /// </summary>
    public int OrderWeight { get; set; }

    public required PersonRole Role { get; set; }
}

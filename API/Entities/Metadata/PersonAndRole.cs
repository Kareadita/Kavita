using API.Entities.Enums;

namespace API.Entities.Metadata;

public class PersonAndRole
{
    public int Id { get; set; }
    public PersonRole Role { get; set; }

    // Relationship
    public Person Person { get; set; }
    public int PersonId { get; set; }
}

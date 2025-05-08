namespace API.Entities.Person;

public class PersonAlias
{
    public int Id { get; set; }
    public required string Alias { get; set; }
    public required string NormalizedAlias { get; set; }

    public int PersonId { get; set; }
    public Person Person { get; set; }
}

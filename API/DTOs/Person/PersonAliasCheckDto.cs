namespace API.DTOs;

public class PersonAliasCheckDto
{
    /// <summary>
    /// The person to check against
    /// </summary>
    public int PersonId { get; set; }
    /// <summary>
    /// The persons name in the form. In case it differs from the one in the database
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// The alias to check
    /// </summary>
    public string Alias { get; set; }
}

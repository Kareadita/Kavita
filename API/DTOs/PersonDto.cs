using API.Entities.Enums;

namespace API.DTOs;

public class PersonDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public PersonRole Role { get; set; }
}

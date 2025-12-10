namespace API.Entities;

public class AppUserExternalSource
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Host { get; set; }
    public required string ApiKey { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }
}

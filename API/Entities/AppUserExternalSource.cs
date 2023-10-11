namespace API.Entities;

public class AppUserExternalSource
{
    public int Id { get; set; }
    public string Host { get; set; }
    public string ApiKey { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }
}

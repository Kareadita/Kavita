using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Entities.User;

public class AppUserExternalSource
{
    public int Id { get; set; }
    public required string Name { get; set; }
    [ExportScrub(ExportScrubKind.Host)]
    public required string Host { get; set; }
    [ExportScrub(ExportScrubKind.Random)]
    public required string ApiKey { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }
}

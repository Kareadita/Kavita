using System;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.Kobo;

public class KoboSyncPoint
{

    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; }

    public DateTime BookSyncTime { get; set; }
    public int BooksSyncedUntilId { get; set; }
    public int BooksSyncedUntilIndex { get; set; }

}

using System;
using System.Collections.Generic;
using API.DTOs.Progress;
using Microsoft.EntityFrameworkCore;

namespace API.Entities.Progress;

/// <summary>
/// Represents a single day's worth of Reading Sessions
/// </summary>
[Index(nameof(DateUtc), IsUnique = true)]
public class AppUserReadingHistory
{
    public int Id { get; set; }
    public DateTime DateUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DailyReadingDataDto Data { get; set; }
    public IList<ClientInfoData> ClientInfoUsed { get; set; }


    public int AppUserId { get; set; }
    public virtual AppUser AppUser { get; set; }
}

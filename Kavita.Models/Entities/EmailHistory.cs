using System;
using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Models.Entities;

/// <summary>
/// Records all emails that are sent from Kavita
/// </summary>
[Index("Sent", "AppUserId", "EmailTemplate", "SendDate")]
public class EmailHistory : IEntityDate
{
    public long Id { get; set; }
    public bool Sent { get; set; }
    public DateTime SendDate { get; set; } = DateTime.UtcNow;
    public string EmailTemplate { get; set; }
    [ExportScrub(ExportScrubKind.TextScrub)]
    public string Subject { get; set; }
    [ExportScrub(ExportScrubKind.Random)]
    public string Body { get; set; }

    public string DeliveryStatus { get; set; }
    [ExportScrub(ExportScrubKind.TextScrub)]
    public string ErrorMessage { get; set; }

    public int AppUserId { get; set; }
    public virtual AppUser AppUser { get; set; }


    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}

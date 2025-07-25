using System;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities;

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
    public string Subject { get; set; }
    public string Body { get; set; }

    public string DeliveryStatus { get; set; }
    public string ErrorMessage { get; set; }

    public int AppUserId { get; set; }
    public virtual AppUser AppUser { get; set; }


    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}

using System;
using Kavita.Models.Entities;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboBookEntitlementDto
{
    public string Id { get; set; }
    public string RevisionId { get; set; }
    public string CrossRevisionId { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }

    public string Accessibility { get; set; } = "Full";
    public string OriginCategory { get; set; } = "Imported";
    public string Status { get; set; } = "Active";

    public bool IsHiddenFromArchive { get; set; } = false;
    public bool IsLocked  { get; set; } = false;
    public bool IsRemoved { get; set; } = false;

    public KoboPeriodDto ActivePeriod { get; set; }

    public KoboBookEntitlementDto(Chapter chapter)
    {
        Id = chapter.Id.ToString();
        RevisionId = Id;
        CrossRevisionId = Id;
        Created = chapter.LastModifiedUtc;
        LastModified = chapter.LastModifiedUtc;
        ActivePeriod = new KoboPeriodDto
        {
            From = DateTime.UtcNow
        };
    }
}

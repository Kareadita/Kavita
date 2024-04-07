using System;

namespace API.DTOs.Progress;

/// <summary>
/// A full progress Record from the DB (not all data, only what's needed for API)
/// </summary>
public class FullProgressDto
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public int PagesRead { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int AppUserId { get; set; }
    public string UserName { get; set; }
}

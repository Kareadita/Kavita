using System;

namespace API.DTOs.Jobs;

public class JobDto
{
    /// <summary>
    /// Job Id
    /// </summary>
    public string Id { get; set; } = default!;
    /// <summary>
    /// Human Readable title for the Job
    /// </summary>
    public string Title { get; set; } = default!;
    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>
    /// Last time the job was run
    /// </summary>
    public DateTime? LastExecution { get; set; }
    public string Cron { get; set; } = default!;
}

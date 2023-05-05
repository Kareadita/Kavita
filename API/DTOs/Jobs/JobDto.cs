using System;

namespace API.DTOs.Jobs;

public class JobDto
{
    /// <summary>
    /// Job Id
    /// </summary>
    public string Id { get; set; }
    /// <summary>
    /// Human Readable title for the Job
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>
    /// Last time the job was run
    /// </summary>
    public DateTime? LastExecution { get; set; }
    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime? CreatedAtUtc { get; set; }
    /// <summary>
    /// Last time the job was run
    /// </summary>
    public DateTime? LastExecutionUtc { get; set; }
    public string Cron { get; set; }
}

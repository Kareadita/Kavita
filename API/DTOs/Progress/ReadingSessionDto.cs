using System;
using System.Collections.Generic;

namespace API.DTOs.Progress;

public sealed record ReadingSessionDto
{
    public int Id { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public IList<ReadingActivityDataDto> ActivityData { get; set; }

    public int UserId { get; set; }
    public string Username { get; set; }
}

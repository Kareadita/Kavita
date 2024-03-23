using System;

namespace API.DTOs.Progress;
#nullable enable

public class UpdateUserProgressDto
{
    public int PageNum { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

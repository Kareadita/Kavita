using System.Collections.Generic;

namespace API.DTOs;

public sealed record DeleteChaptersDto
{
    public IList<int> ChapterIds { get; set; } = default!;
}

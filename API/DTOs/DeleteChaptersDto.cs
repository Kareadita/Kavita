using System.Collections.Generic;

namespace API.DTOs;

public class DeleteChaptersDto
{
    public IList<int> ChapterIds { get; set; } = default!;
}

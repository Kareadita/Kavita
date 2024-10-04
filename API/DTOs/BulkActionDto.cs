using System.Collections.Generic;

namespace API.DTOs;

public class BulkActionDto
{
    public List<int> Ids { get; set; }
    /**
     * If this is a Scan action, will ignore optimizations
     */
    public bool? Force { get; set; }
}

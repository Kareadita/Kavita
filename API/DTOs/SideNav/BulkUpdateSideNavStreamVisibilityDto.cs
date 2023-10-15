using System.Collections.Generic;

namespace API.DTOs.SideNav;

public class BulkUpdateSideNavStreamVisibilityDto
{
    public required IList<int> Ids { get; set; }
    public required bool Visibility { get; set; }
}

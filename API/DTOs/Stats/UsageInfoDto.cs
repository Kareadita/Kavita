using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Stats
{
    public class UsageInfoDto
    {
        public UsageInfoDto()
        {
            FileTypes = new HashSet<string>();
            LibraryTypesCreated = new HashSet<LibInfo>();
        }

        public int UsersCount { get; set; }
        public IEnumerable<string> FileTypes { get; set; }
        public IEnumerable<LibInfo> LibraryTypesCreated { get; set; }
    }

    public class LibInfo
    {
        public LibraryType Type { get; set; }
        public int Count { get; set; }
    }
}

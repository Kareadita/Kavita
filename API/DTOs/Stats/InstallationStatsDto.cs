using System;
using System.Collections.Generic;
using System.Linq;

namespace API.DTOs.Stats
{
    public class InstallationStatsDto
    {
        public string InstallId { get; set; }
        public string Os { get; set; }
        public bool IsDocker { get; set; }
        public string DotnetVersion { get; set; }
        public string KavitaVersion { get; set; }
    }
}

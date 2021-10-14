using System;
using System.Collections.Generic;
using System.Linq;

namespace API.DTOs.Stats
{
    public class UsageStatisticsDto
    {
        public string installId { get; set; }
        public string Os { get; set; }
        public bool isDocker { get; set; }
        public string dotnetVersion { get; set; }
        public string kavitaVersion { get; set; }
    }


    
}

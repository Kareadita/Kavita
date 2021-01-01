using System.Collections.Generic;

namespace API.DTOs
{
    public class SeriesDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string OriginalName { get; set; }
        public string SortName { get; set; }
        public string Summary { get; set; }
    }
}
namespace API.DTOs
{
    public class SeriesDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string OriginalName { get; set; }
        public string SortName { get; set; }
        public string Summary { get; set; }
        public byte[] CoverImage { get; set; }
        
        // Read Progress 
        public int Pages { get; set; }
        public int PagesRead { get; set; }
        //public int VolumesComplete { get; set; }
        //public int TotalVolumes { get; set; }
    }
}
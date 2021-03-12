namespace API.DTOs
{
    public class SearchResultDto
    {
        public int SeriesId { get; init; }
        public string Name { get; init; }
        public string OriginalName { get; init; }
        public string SortName { get; init; }
        public byte[] CoverImage { get; init; } // This should be optional or a thumbImage (much smaller) // TODO: Refactor to lazy loading
        public string CoverImageUrl { get; init; }
        
        // Grouping information
        public string LibraryName { get; set; }
        public int LibraryId { get; set; }
    }
}
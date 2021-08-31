namespace API.DTOs
{
    public class UpdateSeriesDto
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string LocalizedName { get; init; }
        public string SortName { get; init; }
        public string Summary { get; init; }
        public byte[] CoverImage { get; init; }
        public int UserRating { get; set; }
        public string UserReview { get; set; }
        public bool CoverImageLocked { get; set; }
    }
}

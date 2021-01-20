namespace API.DTOs
{
    public class SeriesDto
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string OriginalName { get; init; }
        public string SortName { get; init; }
        public string Summary { get; init; }
        public byte[] CoverImage { get; init; }
        public int Pages { get; init; }
        /// <summary>
        /// Sum of pages read from linked Volumes. Calculated at API-time.
        /// </summary>
        public int PagesRead { get; set; }
        /// <summary>
        /// Rating from logged in user. Calculated at API-time.
        /// </summary>
        public int UserRating { get; set; }
        /// <summary>
        /// Review from logged in user. Calculated at API-time.
        /// </summary>
        public string UserReview { get; set; }
    }
}
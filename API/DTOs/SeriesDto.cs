using System;
using API.Entities.Enums;

namespace API.DTOs
{
    public class SeriesDto
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string OriginalName { get; init; }
        public string LocalizedName { get; init; }
        public string SortName { get; init; }
        public string Summary { get; init; }
        public int Pages { get; init; }
        public bool CoverImageLocked { get; set; }
        /// <summary>
        /// Sum of pages read from linked Volumes. Calculated at API-time.
        /// </summary>
        public int PagesRead { get; set; }
        /// <summary>
        /// DateTime representing last time the series was Read. Calculated at API-time.
        /// </summary>
        public DateTime LatestReadDate { get; set; }
        /// <summary>
        /// Rating from logged in user. Calculated at API-time.
        /// </summary>
        public int UserRating { get; set; }
        /// <summary>
        /// Review from logged in user. Calculated at API-time.
        /// </summary>
        public string UserReview { get; set; }
        public MangaFormat Format { get; set; }

        public DateTime Created { get; set; }

        public bool NameLocked { get; set; }
        public bool SortNameLocked { get; set; }
        public bool LocalizedNameLocked { get; set; }

        public int LibraryId { get; set; }
        public string LibraryName { get; set; }
    }
}

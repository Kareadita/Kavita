namespace API.Data.Metadata
{
    /// <summary>
    /// A representation of a ComicInfo.xml file
    /// </summary>
    /// <remarks>See reference of the loose spec here: https://github.com/Kussie/ComicInfoStandard/blob/main/ComicInfo.xsd</remarks>
    public class ComicInfo
    {
        public string Summary { get; set; }
        public string Title { get; set; }
        public string Series { get; set; }
        public string Number { get; set; }
        public string Volume { get; set; }
        public string Notes { get; set; }
        public string Genre { get; set; }
        public int PageCount { get; set; }
        // ReSharper disable once InconsistentNaming
        public string LanguageISO { get; set; }
        public string Web { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        /// <summary>
        /// Rating based on the content. Think PG-13, R for movies
        /// </summary>
        public string AgeRating { get; set; }
        /// <summary>
        /// User's rating of the content
        /// </summary>
        public float UserRating { get; set; }

        public string AlternateSeries { get; set; }
        public string StoryArc { get; set; }
        public string SeriesGroup { get; set; }
        public string AlternativeSeries { get; set; }
        public string AlternativeNumber { get; set; }

        /// <summary>
        /// This is Epub only: calibre:title_sort
        /// Represents the sort order for the title
        /// </summary>
        public string TitleSort { get; set; }




        /// <summary>
        /// This is the Author. For Books, we map creator tag in OPF to this field. Comma separated if multiple.
        /// </summary>
        public string Writer { get; set; }
        public string Penciller { get; set; }
        public string Inker { get; set; }
        public string Colorist { get; set; }
        public string Letterer { get; set; }
        public string CoverArtist { get; set; }
        public string Editor { get; set; }
        public string Publisher { get; set; }

    }
}

using API.Entities.Enums;

namespace API.Parser
{
    /// <summary>
    /// This represents a single file
    /// </summary>
    public class ParserInfo
    {
        // This can be multiple
        public string Chapters { get; set; } = "";
        public string Series { get; set; } = "";
        // This can be multiple
        public string Volumes { get; set; } = "";
        public string Filename { get; init; } = "";
        public string FullFilePath { get; set; } = "";

        /// <summary>
        /// <see cref="MangaFormat"/> that represents the type of the file (so caching service knows how to cache for reading)
        /// </summary>
        public MangaFormat Format { get; set; } = MangaFormat.Unknown;

        /// <summary>
        /// This can potentially story things like "Omnibus, Color, Full Contact Edition, Extra, Final, etc"
        /// </summary>
        public string Edition { get; set; } = "";

        /// <summary>
        /// If the file contains no volume/chapter information and contains Special Keywords <see cref="Parser.MangaSpecialRegex"/>
        /// </summary>
        public bool IsSpecial { get; set; } = false;

        /// <summary>
        /// Used for specials or books, stores what the UI should show.
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        public bool IsSpecialInfo()
        { 
            return (IsSpecial || (Volumes == "0" && Chapters == "0"));
        }

        /// <summary>
        /// Merges non empty/null properties from info2 into this entity.
        /// </summary>
        /// <param name="info2"></param>
        public void Merge(ParserInfo info2)
        {
            if (info2 == null) return;
            Chapters = string.IsNullOrEmpty(Chapters) || Chapters == "0" ? info2.Chapters: Chapters;
            Volumes = string.IsNullOrEmpty(Volumes) || Volumes == "0" ? info2.Volumes : Volumes;
            Edition = string.IsNullOrEmpty(Edition) ? info2.Edition : Edition;
            Title = string.IsNullOrEmpty(Title) ? info2.Title : Title;
            Series = string.IsNullOrEmpty(Series) ? info2.Series : Series;
            IsSpecial = IsSpecial || info2.IsSpecial;
        }
    }
}
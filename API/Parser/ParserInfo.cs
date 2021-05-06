using API.Entities.Enums;

namespace API.Parser
{
    /// <summary>
    /// This represents a single file
    /// </summary>
    public class ParserInfo
    {
        /// <summary>
        /// Represents the parsed chapters from a file. By default, will be 0 which means nothing could be parsed.
        /// <remarks>The chapters can only be a single float or a range of float ie) 1-2. Mainly floats should be multiples of 0.5 representing specials</remarks>
        /// </summary>
        public string Chapters { get; set; } = "";
        /// <summary>
        /// Represents the parsed series from the file or folder
        /// </summary>
        public string Series { get; set; } = "";
        /// <summary>
        /// Represents the parsed volumes from a file. By default, will be 0 which means that nothing could be parsed.
        /// If Volumes is 0 and Chapters is 0, the file is a special. If Chapters is non-zero, then no volume could be parsed.
        /// <example>Beastars Vol 3-4 will map to "3-4"</example>
        /// <remarks>The volumes can only be a single int or a range of ints ie) 1-2. Float based volumes are not supported.</remarks>
        /// </summary>
        public string Volumes { get; set; } = "";
        /// <summary>
        /// Filename of the underlying file
        /// <example>Beastars v01 (digital).cbz</example>
        /// </summary>
        public string Filename { get; init; } = "";
        /// <summary>
        /// Full filepath of the underlying file
        /// <example>C:/Manga/Beastars v01 (digital).cbz</example>
        /// </summary>
        public string FullFilePath { get; set; } = "";

        /// <summary>
        /// <see cref="MangaFormat"/> that represents the type of the file
        /// <remarks>Mainly used to show in the UI and so caching service knows how to cache for reading.</remarks>
        /// </summary>
        public MangaFormat Format { get; set; } = MangaFormat.Unknown;

        /// <summary>
        /// This can potentially story things like "Omnibus, Color, Full Contact Edition, Extra, Final, etc"
        /// </summary>
        public string Edition { get; set; } = "";

        /// <summary>
        /// If the file contains no volume/chapter information or contains Special Keywords <see cref="Parser.MangaSpecialRegex"/>
        /// </summary>
        public bool IsSpecial { get; set; }

        /// <summary>
        /// Used for specials or books, stores what the UI should show.
        /// <remarks>Manga does not use this field</remarks>
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// If the ParserInfo has the IsSpecial tag or both volumes and chapters are default aka 0
        /// </summary>
        /// <returns></returns>
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
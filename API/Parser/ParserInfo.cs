﻿using API.Entities.Enums;

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
        
        public bool IsSpecialInfo()
        { 
            return (IsSpecial || (Volumes == "0" && Chapters == "0"));
        }
    }
}
using System;
using API.Entities.Enums;

namespace API.DTOs.Reader
{
    public class ChapterInfoDto : IChapterInfoDto
    {

        public string ChapterNumber { get; set; }
        public string VolumeNumber { get; set; }
        public int VolumeId { get; set; }
        public string SeriesName { get; set; }
        public MangaFormat SeriesFormat { get; set; }
        public int SeriesId { get; set; }
        public int LibraryId { get; set; }
        public LibraryType LibraryType { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public int Pages { get; set; }
        public string FileName { get; set; }
        public bool IsSpecial { get; set; }

    }
}

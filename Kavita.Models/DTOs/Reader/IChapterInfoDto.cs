using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Reader;

public interface IChapterInfoDto
{
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
    [EnumDataType(typeof(MangaFormat))]
    public MangaFormat SeriesFormat { get; set; }
    public string SeriesName { get; set; }
    public string ChapterNumber { get; set; }
    public string VolumeNumber { get; set; }
    public int LibraryId { get; set; }
    public int Pages { get; set; }
    public bool IsSpecial { get; set; }
    public string ChapterTitle { get; set; }

}
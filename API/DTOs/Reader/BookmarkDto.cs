using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Reader
{
    public class BookmarkDto
    {
        public int Id { get; set; }
        [Required]
        public int Page { get; set; }
        [Required]
        public int VolumeId { get; set; }
        [Required]
        public int SeriesId { get; set; }
        [Required]
        public int ChapterId { get; set; }
    }
}

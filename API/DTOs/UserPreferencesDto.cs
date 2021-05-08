using API.Entities.Enums;

namespace API.DTOs
{
    public class UserPreferencesDto
    {
        public ReadingDirection ReadingDirection { get; set; }
        public ScalingOption ScalingOption { get; set; }
        public PageSplitOption PageSplitOption { get; set; }
        public bool BookReaderDarkMode { get; set; } = false;
        public int BookReaderMargin { get; set; }
        public int BookReaderLineSpacing { get; set; }
        public int BookReaderFontSize { get; set; }
        public string BookReaderFontFamily { get; set; }
        public bool BookReaderTapToPaginate { get; set; }
    }
}
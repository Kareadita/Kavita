using API.DTOs.Theme;
using API.Entities.Enums;

namespace API.DTOs
{
    public class UserPreferencesDto
    {
        public ReadingDirection ReadingDirection { get; set; }
        public ScalingOption ScalingOption { get; set; }
        public PageSplitOption PageSplitOption { get; set; }
        public ReaderMode ReaderMode { get; set; }
        public bool AutoCloseMenu { get; set; }
        public int BookReaderMargin { get; set; }
        public int BookReaderLineSpacing { get; set; }
        public int BookReaderFontSize { get; set; }
        public string BookReaderFontFamily { get; set; }
        public bool BookReaderTapToPaginate { get; set; }
        public ReadingDirection BookReaderReadingDirection { get; set; }
        public SiteThemeDto Theme { get; set; }
        public string BookReaderThemeName { get; set; }
        public BookPageLayoutMode BookReaderLayoutMode { get; set; }
    }
}

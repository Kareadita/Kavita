using API.Entities.Enums;

namespace API.Entities
{
    public class AppUserPreferences
    {
        public int Id { get; set; }
        /// <summary>
        /// Manga Reader Option: What direction should the next/prev page buttons go
        /// </summary>
        public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;
        /// <summary>
        /// Manga Reader Option: How should the image be scaled to screen
        /// </summary>
        public ScalingOption ScalingOption { get; set; } = ScalingOption.Automatic;
        /// <summary>
        /// Manga Reader Option: Which side of a split image should we show first
        /// </summary>
        public PageSplitOption PageSplitOption { get; set; } = PageSplitOption.SplitRightToLeft;

        /// <summary>
        /// Book Reader Option: Should the background color be dark
        /// </summary>
        public bool BookReaderDarkMode { get; set; } = false;
        /// <summary>
        /// Book Reader Option: Override extra Margin
        /// </summary>
        public int BookReaderMargin { get; set; } = 15;
        /// <summary>
        /// Book Reader Option: Override line-height
        /// </summary>
        public int BookReaderLineSpacing { get; set; } = 100;
        /// <summary>
        /// Book Reader Option: Override font size
        /// </summary>
        public int BookReaderFontSize { get; set; } = 100;
        /// <summary>
        /// Book Reader Option: Maps to the default Kavita font-family (inherit) or an override
        /// </summary>
        public string BookReaderFontFamily { get; set; } = "default";
        /// <summary>
        /// Book Reader Option: Allows tapping on side of screens to paginate
        /// </summary>
        public bool BookReaderTapToPaginate { get; set; } = false;
        
        
        
        public AppUser AppUser { get; set; }
        public int AppUserId { get; set; }
    }
}
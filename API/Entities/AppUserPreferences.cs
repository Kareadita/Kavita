using API.Entities.Enums;

namespace API.Entities
{
    public class AppUserPreferences
    {
        public int Id { get; set; }
        public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;
        public ScalingOption ScalingOption { get; set; } = ScalingOption.FitToHeight;
        public PageSplitOption PageSplitOption { get; set; } = PageSplitOption.SplitRightToLeft;
        /// <summary>
        /// Whether UI hides read Volumes on Details page
        /// </summary>
        public bool HideReadOnDetails { get; set; } = false;
        
        
        
        public AppUser AppUser { get; set; }
        public int AppUserId { get; set; }
    }
}
using API.Entities.Enums;

namespace API.DTOs
{
    public class UserPreferencesDto
    {
        public ReadingDirection ReadingDirection { get; set; }
        public ScalingOption ScalingOption { get; set; }
        public PageSplitOption PageSplitOption { get; set; }
        /// <summary>
        /// Whether UI hides read Volumes on Details page
        /// </summary>
        public bool HideReadOnDetails { get; set; }
    }
}
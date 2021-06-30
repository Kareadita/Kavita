using System.ComponentModel;

namespace API.Entities.Enums
{
    public enum ReaderMode
    {
        [Description("Left and Right")]
        MANGA_LR = 0,
        [Description("Up and Down")]
        MANGA_UP = 1,
        [Description("Webtoon")]
        WEBTOON = 2
    }
}
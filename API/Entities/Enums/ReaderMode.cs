﻿using System.ComponentModel;

namespace API.Entities.Enums
{
    public enum ReaderMode
    {
        [Description("Left and Right")]
        // ReSharper disable once InconsistentNaming
        MANGA_LR = 0,
        [Description("Up and Down")]
        // ReSharper disable once InconsistentNaming
        MANGA_UP = 1,
        [Description("Webtoon")]
        // ReSharper disable once InconsistentNaming
        WEBTOON = 2
    }
}

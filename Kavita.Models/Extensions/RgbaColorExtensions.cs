using System;
using Kavita.Models.Entities;

namespace Kavita.Models.Extensions;

public static class RgbaColorExtensions
{
    public static string ToHex(this RgbaColor color, bool alpha = true)
    {
        if (alpha)
        {
            var a = (int)Math.Round(Math.Clamp(color.A, 0f, 1f) * 255f);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{a:X2}";
        }

        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}

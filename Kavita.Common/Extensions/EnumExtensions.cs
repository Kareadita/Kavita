using System.ComponentModel;

namespace Kavita.Common.Extensions;

public static class EnumExtensions
{
    public static string ToDescription<TEnum>(this TEnum value) where TEnum : struct
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);

        if (fi == null)
        {
            return value.ToString();
        }

        var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

        return attributes is {Length: > 0} ? attributes[0].Description : value.ToString();
    }
}

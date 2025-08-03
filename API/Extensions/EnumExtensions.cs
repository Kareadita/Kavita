#nullable enable
using System;
using System.ComponentModel;
using System.Reflection;

namespace API.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// Extension on Enum.TryParse which also tried matching on the description attribute
    /// </summary>
    /// <returns>if a match was found</returns>
    /// <remarks>First tries Enum.TryParse then fall back to the more expensive operation</remarks>
    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (Enum.TryParse(value, out result))
        {
            return true;
        }

        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var description = field.GetCustomAttribute<DescriptionAttribute>()?.Description;

            if (!string.IsNullOrEmpty(description) &&
                string.Equals(description, value, StringComparison.OrdinalIgnoreCase))
            {
                result = (TEnum)field.GetValue(null)!;
                return true;
            }
        }

        return false;
    }
}

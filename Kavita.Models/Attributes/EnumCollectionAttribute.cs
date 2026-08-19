using System;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.Attributes;

/// <summary>
/// An attribute to apply on fields in our DTOs to validate all passed values in the collection is valid enum types
/// </summary>
/// <param name="enumType"></param>
/// <param name="allowNull"></param>
public class EnumCollectionAttribute(Type enumType, bool allowNull = false): ValidationAttribute
{

    public override bool IsValid(object value)
    {
        if (value == null)
            return allowNull;

        if (value is not IEnumerable enumerable)
            return false;

        foreach (var item in enumerable)
        {
            if (item == null && !allowNull)
                return false;

            if (item != null && !Enum.IsDefined(enumType, item))
                return false;
        }

        return true;
    }
}

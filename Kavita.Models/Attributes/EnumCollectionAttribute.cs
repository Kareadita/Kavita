using System;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.Attributes;

public class EnumCollectionAttribute(Type enumType, bool allowNull = false): ValidationAttribute
{

    public override bool IsValid(object value)
    {
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

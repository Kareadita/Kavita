#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace API.Tests.Helpers;

public static class RandfHelper
{
    private static readonly Random Random = new ();

    /// <summary>
    /// Returns true if all simple fields are equal
    /// </summary>
    /// <param name="obj1"></param>
    /// <param name="obj2"></param>
    /// <param name="ignoreFields">fields to ignore, note that the names are very weird sometimes</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static bool AreSimpleFieldsEqual(object obj1, object obj2, IList<string> ignoreFields)
    {
        if (obj1 == null || obj2 == null)
            throw new ArgumentNullException("Neither object can be null.");

        var type1 = obj1.GetType();
        var type2 = obj2.GetType();

        if (type1 != type2)
            throw new ArgumentException("Objects must be of the same type.");

        var fields = type1.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            if (field.IsInitOnly) continue;
            if (ignoreFields.Contains(field.Name)) continue;

            var fieldType = field.FieldType;

            if (!IsRelevantType(fieldType)) continue;

            var value1 = field.GetValue(obj1);
            var value2 = field.GetValue(obj2);

            if (!Equals(value1, value2))
            {
                throw new ArgumentException("Fields must be of the same type: " +  field.Name + " was  " + value1 + " and  " + value2);
            }
        }

        return true;
    }

    private static bool IsRelevantType(Type type)
    {
        return type.IsPrimitive
               || type == typeof(string)
               || type.IsEnum;
    }

    /// <summary>
    /// Sets all simple fields of the given object to a random value
    /// </summary>
    /// <param name="obj"></param>
    /// <remarks>Simple is, primitive, string, or enum</remarks>
    /// <exception cref="ArgumentNullException"></exception>
    public static void SetRandomValues(object obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        Type type = obj.GetType();
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            if (field.IsInitOnly) continue; // Skip readonly fields

            var value = GenerateRandomValue(field.FieldType, field.GetValue(obj));
            if (value != null)
            {
                field.SetValue(obj, value);
            }
        }
    }

    private static object? GenerateRandomValue(Type type, object? value)
    {
        if (type == typeof(int))
            return Random.Next();
        if (type == typeof(float))
            return (float)Random.NextDouble() * 100;
        if (type == typeof(double))
            return Random.NextDouble() * 100;
        if (type == typeof(bool))
            return value != null ? !(bool)value : Random.Next(2) == 1;
        if (type == typeof(char))
            return (char)Random.Next('A', 'Z' + 1);
        if (type == typeof(byte))
            return (byte)Random.Next(0, 256);
        if (type == typeof(short))
            return (short)Random.Next(short.MinValue, short.MaxValue);
        if (type == typeof(long))
            return (long)(Random.NextDouble() * long.MaxValue);
        if (type == typeof(string))
            return GenerateRandomString(10);
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(Random.Next(values.Length));
        }

        // Unsupported type
        return null;
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }
}

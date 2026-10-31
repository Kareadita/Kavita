using System;

namespace Kavita.Models.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class JsonColumnAttribute : Attribute
{
    public string ColumnType { get; set; } = "TEXT";
}

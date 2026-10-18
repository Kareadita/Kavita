using System;
using System.Reflection;
using System.Text.Json;
using Kavita.Models.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kavita.Database.Extensions;

internal static class JsonColumnModelBuilderExtensions
{
    public static void ApplyJsonColumns(this ModelBuilder modelBuilder, JsonSerializerOptions? jsonOptions = null)
    {
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var clrProperty in entityType.ClrType.GetProperties())
            {
                var attr = clrProperty.GetCustomAttribute<JsonColumnAttribute>();
                if (attr is null) continue;

                var propertyType = clrProperty.PropertyType;

                var converter = (ValueConverter)Activator.CreateInstance(
                    typeof(JsonValueConverter<>).MakeGenericType(propertyType), jsonOptions)!;

                var comparer = (ValueComparer)Activator.CreateInstance(
                    typeof(JsonValueComparer<>).MakeGenericType(propertyType), jsonOptions)!;

                var propertyBuilder = modelBuilder.Entity(entityType.ClrType).Property(clrProperty.Name);

                propertyBuilder.HasConversion(converter);
                propertyBuilder.Metadata.SetValueComparer(comparer);
                propertyBuilder.HasColumnType(attr.ColumnType);


                if (!IsNullable(clrProperty, nullabilityContext) && propertyType.GetConstructor(Type.EmptyTypes) != null)
                {
                    var defaultValue = Activator.CreateInstance(propertyType);
                    propertyBuilder.HasDefaultValue(defaultValue);
                }
            }
        }
    }

    private static bool IsNullable(PropertyInfo property, NullabilityInfoContext context)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            return true;

        if (property.PropertyType.IsValueType)
            return false;

        var info = context.Create(property);
        return info.WriteState is NullabilityState.Nullable || info.ReadState is NullabilityState.Nullable;
    }
}


internal class JsonValueConverter<T>(JsonSerializerOptions? options = null) : ValueConverter<T, string>(
    v => JsonSerializer.Serialize(v, options),
    v => JsonSerializer.Deserialize<T>(v, options)!);

internal class JsonValueComparer<T>(JsonSerializerOptions? options = null) : ValueComparer<T>(
    (l, r) => JsonSerializer.Serialize(l, options) == JsonSerializer.Serialize(r, options),
    v => v == null ? 0 : JsonSerializer.Serialize(v, options).GetHashCode(),
    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, options), options)!);

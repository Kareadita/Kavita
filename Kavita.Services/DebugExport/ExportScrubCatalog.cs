using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Kavita.Services.DebugExport;

/// <summary>
/// The scrub class of every column, JSON field and server setting, from <see cref="ExportScrubAttribute"/>
/// and a fallback for types Kavita does not own. Anything else is exported as is
/// </summary>
public static class ExportScrubCatalog
{
    // Declared on Identity's base classes, so our attribute can't be put on them
    private static readonly Dictionary<string, ExportScrubKind> FrameworkFallback = new()
    {
        ["AppUser.UserName"] = ExportScrubKind.Username,
        ["AppUser.NormalizedUserName"] = ExportScrubKind.Username,
        ["AppUser.Email"] = ExportScrubKind.Email,
        ["AppUser.NormalizedEmail"] = ExportScrubKind.Email,
        ["AppUser.PasswordHash"] = ExportScrubKind.Random,
        ["AppUser.SecurityStamp"] = ExportScrubKind.Guid,
        ["AppUser.PhoneNumber"] = ExportScrubKind.Phone,
        ["IdentityUserToken<Int32>.Value"] = ExportScrubKind.Random,
        ["IdentityUserLogin<Int32>.ProviderKey"] = ExportScrubKind.Guid,
        ["IdentityUserClaim<Int32>.ClaimValue"] = ExportScrubKind.Random,
    };

    private static readonly HashSet<Type> DroppedTables = [typeof(DataProtectionKey)];

    public static IReadOnlyList<Entry> Classify(IModel model)
    {
        var entries = new List<Entry>();
        var jsonTypes = new Queue<Type>();

        foreach (var entityType in model.GetEntityTypes())
        {
            var entityName = entityType.HasSharedClrType ? entityType.Name : TypeName(entityType.ClrType);
            var dropped = DroppedTables.Contains(entityType.ClrType);

            foreach (var property in entityType.GetProperties())
            {
                var name = $"{entityName}.{property.Name}";
                if (dropped)
                {
                    entries.Add(new Entry(name, ExportScrubKind.DropTable, null, Source.Fallback, Area.Column, IsText(property.ClrType)));
                    continue;
                }

                if (property.GetValueConverter() != null && JsonObjectType(property.ClrType) is { } storedType)
                {
                    jsonTypes.Enqueue(storedType);
                    continue;
                }

                var attribute = property.PropertyInfo?.GetCustomAttribute<ExportScrubAttribute>();
                entries.Add(Resolve(name, attribute, Area.Column, IsText(property.ClrType)));
            }

            foreach (var complexProperty in entityType.GetComplexProperties())
            {
                jsonTypes.Enqueue(JsonObjectType(complexProperty.ClrType) ?? complexProperty.ClrType);
            }
        }

        foreach (var field in typeof(ServerSettingKey).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attribute = field.GetCustomAttribute<ExportScrubAttribute>();
            entries.Add(Resolve($"{nameof(ServerSettingKey)}.{field.Name}", attribute, Area.ServerSetting, true));
            if (attribute?.JsonType != null)
            {
                jsonTypes.Enqueue(attribute.JsonType);
            }
        }

        var walked = new HashSet<Type>();
        while (jsonTypes.TryDequeue(out var type))
        {
            if (!walked.Add(type)) continue;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);
            foreach (var property in properties)
            {
                if (JsonObjectType(property.PropertyType) is { } nested)
                {
                    jsonTypes.Enqueue(nested);
                    continue;
                }

                var attribute = property.GetCustomAttribute<ExportScrubAttribute>();
                entries.Add(Resolve($"{TypeName(type)}.{property.Name}", attribute, Area.JsonField, IsText(property.PropertyType)));
            }
        }

        return entries;
    }

    private static Entry Resolve(string name, ExportScrubAttribute? attribute, Area area, bool isText)
    {
        if (attribute != null) return new Entry(name, attribute.Kind, attribute.JsonType, Source.Attribute, area, isText);
        if (FrameworkFallback.TryGetValue(name, out var kind)) return new Entry(name, kind, null, Source.Fallback, area, isText);

        return new Entry(name, null, null, Source.Unmarked, area, isText);
    }

    /// <summary>
    /// The Kavita type a JSON value holds, looking through nullables, lists and dictionary values. Null for strings, primitives and enums
    /// </summary>
    private static Type? JsonObjectType(Type type)
    {
        var element = ElementTypes(type).Last();
        var isKavitaType = element.Namespace?.StartsWith("Kavita.", StringComparison.Ordinal) == true;
        return isKavitaType && !element.IsEnum ? element : null;
    }

    /// <summary>
    /// A string, or a list or dictionary with strings in it. Only text can carry a token, an address or a name
    /// </summary>
    private static bool IsText(Type type) => ElementTypes(type).Contains(typeof(string));

    /// <summary>
    /// The type itself, or for a list its element, or for a dictionary its key then its value. Nullables unwrapped
    /// </summary>
    private static List<Type> ElementTypes(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return [type];

        var generics = type.GetInterfaces().Append(type).Where(i => i.IsGenericType).ToList();
        var dictionary = generics.FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                                                     || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        var enumerable = generics.FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        List<Type> elements;
        if (dictionary != null)
        {
            elements = [.. dictionary.GetGenericArguments()];
        }
        else if (type.IsArray)
        {
            elements = [type.GetElementType()!];
        }
        else if (enumerable != null)
        {
            elements = [enumerable.GetGenericArguments()[0]];
        }
        else
        {
            elements = [type];
        }

        return elements.Select(t => Nullable.GetUnderlyingType(t) ?? t).ToList();
    }

    private static string TypeName(Type type)
    {
        var prefix = type.IsNested ? TypeName(type.DeclaringType!) + "." : string.Empty;
        if (!type.IsGenericType) return prefix + type.Name;

        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{prefix}{name}<{string.Join(",", type.GetGenericArguments().Select(a => a.Name))}>";
    }

    /// <param name="Kind">Null when the value is exported as is</param>
    /// <param name="IsText">A string or a collection of strings, the only values that can leak a secret</param>
    public sealed record Entry(string Name, ExportScrubKind? Kind, Type? JsonType, Source Source, Area Area, bool IsText);

    public enum Source
    {
        Attribute,
        Fallback,
        Unmarked,
    }

    public enum Area
    {
        Column,
        JsonField,
        ServerSetting,
    }
}

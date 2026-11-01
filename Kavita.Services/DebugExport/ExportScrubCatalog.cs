using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Every column, JSON field and server setting the catalog looked at, marked or not
    /// </summary>
    public static IReadOnlyList<Entry> Classify(IModel model) => Build(model).Entries;

    /// <summary>
    /// Reads the EF model and <see cref="ServerSettingKey"/>, then follows every JSON column and setting down into the
    /// types it stores, to list what the scrub has to rewrite
    /// </summary>
    public static Plan Build(IModel model)
    {
        var entries = new List<Entry>();
        var tables = new List<TableScrub>();
        var settings = new List<SettingScrub>();
        var jsonTypes = new Queue<Type>();

        foreach (var entityType in model.GetEntityTypes())
        {
            var entityName = entityType.HasSharedClrType ? entityType.Name : TypeName(entityType.ClrType);
            var dropped = DroppedTables.Contains(entityType.ClrType);
            var columns = new List<ColumnScrub>();

            foreach (var property in entityType.GetProperties())
            {
                var name = $"{entityName}.{property.Name}";
                if (dropped)
                {
                    entries.Add(new Entry(name, ExportScrubKind.DropTable, null, Source.Fallback, Area.Column, IsTextType(property.ClrType)));
                    continue;
                }

                if (property.GetValueConverter() != null && JsonObjectType(property.ClrType) is { } storedType)
                {
                    jsonTypes.Enqueue(storedType);
                    columns.Add(new ColumnScrub(property.GetColumnName(), ExportScrubKind.Json, property.ClrType));
                    continue;
                }

                var attribute = property.PropertyInfo?.GetCustomAttribute<ExportScrubAttribute>();
                var entry = Resolve(name, attribute, Area.Column, IsTextType(property.ClrType));
                entries.Add(entry);
                if (entry.Kind == ExportScrubKind.Json)
                {
                    var selector = JsonSelector(entityType, attribute!);
                    foreach (var type in selector?.Selector.AllTypes ?? [attribute!.JsonType])
                    {
                        jsonTypes.Enqueue(type);
                    }
                    columns.Add(new ColumnScrub(property.GetColumnName(), ExportScrubKind.Json, attribute!.JsonType, selector));
                }
                else if (entry.Kind is { } kind)
                {
                    columns.Add(new ColumnScrub(property.GetColumnName(), kind, null));
                }
            }

            foreach (var complexProperty in entityType.GetComplexProperties())
            {
                jsonTypes.Enqueue(JsonObjectType(complexProperty.ClrType) ?? complexProperty.ClrType);
                columns.Add(new ColumnScrub(complexProperty.ComplexType.GetContainerColumnName()!, ExportScrubKind.Json, complexProperty.ClrType));
            }

            var scrubbed = columns.Where(c => c.Kind != ExportScrubKind.Json || c.StoredTypes.Any(t => HoldsScrubbedField(t, []))).ToList();
            if (dropped || scrubbed.Count > 0)
            {
                tables.Add(new TableScrub(entityType.GetTableName()!, entityName, dropped, scrubbed, FindUserColumn(entityType)));
            }
        }

        foreach (var field in typeof(ServerSettingKey).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attribute = field.GetCustomAttribute<ExportScrubAttribute>();
            entries.Add(Resolve($"{nameof(ServerSettingKey)}.{field.Name}", attribute, Area.ServerSetting, true));
            if (attribute == null) continue;

            settings.Add(new SettingScrub((ServerSettingKey)field.GetValue(null)!, attribute.Kind, attribute.JsonType));
            if (attribute.JsonType != null)
            {
                jsonTypes.Enqueue(attribute.JsonType);
            }
        }

        var walked = new HashSet<Type>();
        while (jsonTypes.TryDequeue(out var type))
        {
            if (!walked.Add(type)) continue;

            foreach (var property in JsonProperties(type))
            {
                if (JsonObjectType(property.PropertyType) is { } nested)
                {
                    jsonTypes.Enqueue(nested);
                    continue;
                }

                var attribute = property.GetCustomAttribute<ExportScrubAttribute>();
                entries.Add(Resolve(FieldName(type, property.Name), attribute, Area.JsonField, IsTextType(property.PropertyType)));
            }
        }

        var jsonKinds = entries
            .Where(e => e.Area == Area.JsonField && e.Kind != null)
            .ToDictionary(e => e.Name, e => e.Kind!.Value);
        EnsureKindsFit(entries);

        return new Plan(entries, tables, settings, jsonKinds);
    }

    /// <summary>
    /// Throws on a kind marked where the scrub never applies it: JSON fields and settings get no text pass, and a single value can't be
    /// parsed as JSON or dropped like a table
    /// </summary>
    internal static void EnsureKindsFit(IEnumerable<Entry> entries)
    {
        var misplaced = entries
            .Where(e => e.Kind is { } kind
                        && (e.Area == Area.JsonField && kind is ExportScrubKind.TextScrub or ExportScrubKind.Json or ExportScrubKind.DropTable
                            || e.Area == Area.ServerSetting && kind is ExportScrubKind.TextScrub or ExportScrubKind.DropTable))
            .Select(e => $"{e.Name} ({e.Kind})")
            .ToList();
        if (misplaced.Count == 0) return;

        throw new InvalidOperationException($"Marked with a kind the debug export can't apply there: {string.Join(", ", misplaced)}");
    }

    /// <summary>
    /// The properties System.Text.Json writes for a stored type
    /// </summary>
    internal static IEnumerable<PropertyInfo> JsonProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);

    private static bool HoldsScrubbedField(Type type, HashSet<Type> visiting)
    {
        var objectType = JsonObjectType(type);
        if (objectType == null || !visiting.Add(objectType)) return false;

        return JsonProperties(objectType).Any(p =>
            p.GetCustomAttribute<ExportScrubAttribute>() != null || HoldsScrubbedField(p.PropertyType, visiting));
    }

    private static string FieldName(Type type, string property) => $"{TypeName(type)}.{property}";

    /// <summary>
    /// The column holding the row's <see cref="AppUser"/> id: the key on <c>AspNetUsers</c>, else the first foreign key to it
    /// </summary>
    private static string? FindUserColumn(IEntityType entityType)
    {
        if (entityType.ClrType == typeof(AppUser)) return entityType.FindPrimaryKey()!.Properties[0].GetColumnName();

        return entityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(AppUser))?
            .Properties[0].GetColumnName();
    }

    private static JsonTypeSelector? JsonSelector(IEntityType entityType, ExportScrubAttribute attribute)
    {
        if (attribute.JsonTypeSelector == null) return null;

        var selector = (IExportJsonTypeSelector) Activator.CreateInstance(attribute.JsonTypeSelector)!;
        var columns = selector.Discriminators.Select(p => entityType.FindProperty(p)!.GetColumnName()).ToList();
        return new JsonTypeSelector(selector, columns);
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
    internal static Type? JsonObjectType(Type type)
    {
        var element = ElementTypes(type).Last();
        var isKavitaType = element.Namespace?.StartsWith("Kavita.", StringComparison.Ordinal) == true;
        return isKavitaType && !element.IsEnum ? element : null;
    }

    /// <summary>
    /// A string, or a list or dictionary with strings in it. Only text can carry a token, an address or a name
    /// </summary>
    private static bool IsTextType(Type type) => ElementTypes(type).Contains(typeof(string));

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

    /// <param name="Name"><c>AppUser.Email</c>, <c>ServerSettingKey.OidcConfiguration</c> or, for a JSON field, <c>OidcConfigDto.Secret</c></param>
    /// <param name="Kind">Null when the value is exported as is</param>
    /// <param name="IsText">A string or a collection of strings, the only values that can leak a secret</param>
    public sealed record Entry(string Name, ExportScrubKind? Kind, Type? JsonType, Source Source, Area Area, bool IsText);

    /// <param name="Entries">Everything looked at, including values exported as is</param>
    /// <param name="Tables">Only tables that are dropped or have a column to rewrite</param>
    /// <param name="JsonFieldKinds">Keyed by <c>Type.Property</c>, read through <see cref="JsonFieldKind"/></param>
    public sealed record Plan(
        IReadOnlyList<Entry> Entries,
        IReadOnlyList<TableScrub> Tables,
        IReadOnlyList<SettingScrub> Settings,
        IReadOnlyDictionary<string, ExportScrubKind> JsonFieldKinds)
    {
        /// <returns>Null when the field is exported as is</returns>
        public ExportScrubKind? JsonFieldKind(Type type, string property) =>
            JsonFieldKinds.TryGetValue(FieldName(type, property), out var kind) ? kind : null;
    }

    /// <param name="EntityName">As in <see cref="Entry.Name"/>, <c>AppUser</c> for the <c>AspNetUsers</c> table</param>
    /// <param name="Drop">Delete every row and ignore <paramref name="Columns"/></param>
    /// <param name="Columns">Only marked columns, and JSON columns whose stored type has a marked field</param>
    /// <param name="UserColumn">The row's <see cref="AppUser"/> id, so a username on it becomes <c>user-&lt;Id&gt;</c>. Null when the table has none</param>
    public sealed record TableScrub(string Table, string EntityName, bool Drop, IReadOnlyList<ColumnScrub> Columns, string? UserColumn = null);

    /// <param name="JsonType">The property's CLR type when <paramref name="Kind"/> is <see cref="ExportScrubKind.Json"/></param>
    /// <param name="Selector">Set when the JSON type depends on other columns of the row, and then <paramref name="JsonType"/> is null</param>
    public sealed record ColumnScrub(string Column, ExportScrubKind Kind, Type? JsonType, JsonTypeSelector? Selector = null)
    {
        public IReadOnlyList<Type> StoredTypes => Selector?.Selector.AllTypes ?? (JsonType == null ? [] : [JsonType]);
    }

    /// <param name="Columns">The column of each <see cref="IExportJsonTypeSelector.Discriminators"/> entry, in the same order</param>
    public sealed record JsonTypeSelector(IExportJsonTypeSelector Selector, IReadOnlyList<string> Columns);

    public sealed record SettingScrub(ServerSettingKey Key, ExportScrubKind Kind, Type? JsonType);

    public enum Source
    {
        /// <summary>
        /// <see cref="ExportScrubAttribute"/> on the property or setting key
        /// </summary>
        Attribute,
        /// <summary>
        /// <see cref="FrameworkFallback"/> or <see cref="DroppedTables"/>, for types Kavita does not own
        /// </summary>
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

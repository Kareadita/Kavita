using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kavita.Server.Swagger;

/// <summary>
/// Enriches generated schemas for C# enums so downstream OpenAPI viewers
/// (Swagger UI, Docusaurus OpenAPI, etc.) can render human-readable names and
/// descriptions instead of bare integers.
///
/// The wire format is intentionally left untouched (integers), so existing API
/// consumers — OPDS clients, plugins, Tachiyomi — are not affected. This filter
/// only annotates the generated OpenAPI document with:
///   - <c>x-enum-varnames</c>     — parallel array of C# member names
///   - <c>x-enum-descriptions</c> — parallel array of [Description] values
///   - A readable <c>description</c> listing each value.
/// </summary>
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        if (type is not { IsEnum: true }) return;
        if (schema is not OpenApiSchema concrete) return;

        var varNames = new JsonArray();
        var descriptions = new JsonArray();
        var descriptionBuilder = new StringBuilder();

        var existingDescription = concrete.Description;
        if (!string.IsNullOrWhiteSpace(existingDescription))
        {
            descriptionBuilder.AppendLine(existingDescription);
            descriptionBuilder.AppendLine();
        }

        descriptionBuilder.AppendLine("Members:");

        foreach (var value in Enum.GetValues(type))
        {
            var memberName = value.ToString() ?? string.Empty;
            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
            var descriptionAttr = field?.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var display = string.IsNullOrWhiteSpace(descriptionAttr) ? memberName : descriptionAttr;

            varNames.Add(memberName);
            descriptions.Add(display);

            // Format: "- 0 — Manga (Manga)" when the [Description] differs from the identifier,
            // otherwise "- 0 — Manga".
            var numericValue = Convert.ToInt64(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            descriptionBuilder.Append("- `").Append(numericValue).Append("` — ").Append(display);
            if (!string.Equals(display, memberName, StringComparison.Ordinal))
            {
                descriptionBuilder.Append(" (").Append(memberName).Append(')');
            }
            descriptionBuilder.AppendLine();
        }

        concrete.Description = descriptionBuilder.ToString().TrimEnd();
        concrete.Extensions ??= new System.Collections.Generic.Dictionary<string, IOpenApiExtension>();
        concrete.Extensions["x-enum-varnames"] = new JsonNodeExtension(varNames);
        concrete.Extensions["x-enum-descriptions"] = new JsonNodeExtension(descriptions);
    }
}

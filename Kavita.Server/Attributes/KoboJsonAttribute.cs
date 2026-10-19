using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Kavita.Server.Attributes;

/// <summary>
/// Attribute to read & write JSON as Kobo wants it
/// </summary>
public class KoboJsonAttribute: ActionFilterAttribute
{

    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = new PascalJsonNamingPolicy(),
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters =
        {
            new JsonStringEnumConverter(new PascalJsonNamingPolicy()),
            new KoboUtcDateTimeConverter(),
        },
    };

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult result)
        {
            result.Formatters.Clear();
            result.Formatters.Add(new SystemTextJsonOutputFormatter(JsonSerializerOptions));
        }
    }

    private class PascalJsonNamingPolicy: JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            if (name.Length == 1)
            {
                return char.ToUpper(name[0]) + string.Empty;
            }

            var camelCase = CamelCase.ConvertName(name);
            return char.ToUpper(camelCase[0]) + camelCase[1..];
        }
    }

    private class KoboUtcDateTimeConverter: JsonConverter<DateTime>
    {
        private const string KoboDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (DateTime.TryParse(str, null, DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            }
            throw new JsonException($"Unable to parse DateTime: {str}");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            value = value.Kind != DateTimeKind.Utc ? value.ToUniversalTime() : value;
            writer.WriteStringValue(value.ToString(KoboDateTimeFormat));
        }
    }
}

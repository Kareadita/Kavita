using System.Text.Json.Nodes;

namespace Kavita.Common.Extensions;

public static class JsonNodeExtensions
{
    public static string? GetString(this JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;
    }
}

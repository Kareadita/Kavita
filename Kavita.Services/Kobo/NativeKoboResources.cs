using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Kavita.Services.Kobo;

/// <summary>
/// Calibre-Web <c>NATIVE_KOBO_RESOURCES()</c> fallback map used when store proxy is off.
/// </summary>
internal static class NativeKoboResources
{
    private static readonly Lazy<string> TemplateJson = new(LoadTemplateJson);

    public static JsonObject CreateCopy()
    {
        return JsonNode.Parse(TemplateJson.Value)!.AsObject();
    }

    private static string LoadTemplateJson()
    {
        var assembly = typeof(NativeKoboResources).Assembly;
        const string resourceName = "Kavita.Services.Kobo.native_kobo_resources.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

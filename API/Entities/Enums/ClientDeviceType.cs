using System.ComponentModel;

namespace API.Entities.Enums;

public enum ClientDeviceType
{
    [Description("Unknown")]
    Unknown = 0,
    [Description("Web Browser")]
    WebBrowser = 1,
    /// <summary>
    /// WebApp is Kavita's explicit web UI, otherwise a generic browser will be WebBrowser
    /// </summary>
    [Description("Web App")]
    WebApp = 2,
    [Description("KOReader")]
    KoReader = 3,
    [Description("Panels")]
    Panels = 4,
    [Description("Librera")]
    Librera = 5,
    /// <summary>
    /// Generic fallback to anything consuming opds/ url
    /// </summary>
    [Description("OPDS Client")]
    OpdsClient = 6
}

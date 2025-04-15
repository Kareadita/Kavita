namespace API.DTOs;

public class KavitaLocale
{
    public string FileName { get; set; } // Key
    public string RenderName { get; set; }
    public float TranslationCompletion { get; set; }
    public bool IsRtL { get; set; }
    public string Hash { get; set; } // ETAG hash so I can run my own localization busting implementation
}

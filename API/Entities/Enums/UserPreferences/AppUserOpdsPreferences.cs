namespace API.Entities.Enums.UserPreferences;

public class AppUserOpdsPreferences
{
    /// <summary>
    /// Embed Progress Indicator in Title
    /// </summary>
    public bool EmbedProgressIndicator { get; set; } = true;
    /// <summary>
    /// Insert a "Continue From X" entry in OPDS fields to avoid finding your last reading point (Emulates Kavita's Continue button)
    /// </summary>
    public bool IncludeContinueFrom { get; set; } = true;

}

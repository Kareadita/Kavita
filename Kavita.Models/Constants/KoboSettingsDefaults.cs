namespace Kavita.Models.Constants;

/// <summary>
/// Single source for Kobo sync/conversion setting defaults and bounds.
/// Seed, <c>ServerSettingDto</c>, and runtime validation all reference these.
/// </summary>
public static class KoboSettingsDefaults
{
    public const int SyncPageSize = 100;
    public const int MinSyncPageSize = 1;
    public const int MaxSyncPageSize = 1000;
    public const int ConvertTimeBudgetSeconds = 30;
    public const string CacheFolderName = "kobo";
}

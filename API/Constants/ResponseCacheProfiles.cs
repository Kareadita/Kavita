namespace API.Constants;

public static class ResponseCacheProfiles
{
    public const string Minute = "Minute";
    public const string FiveMinute = "5Minute";
    public const string TenMinute = "10Minute";
    public const string Hour = "Hour";
    public const string Month = "Month";
    /// <summary>
    /// 6 hour long cache as underlying API is expensive
    /// </summary>
    public const string Statistics = "Statistics";
    /// <summary>
    /// 4 Hours
    /// </summary>
    public const string LicenseCache = "LicenseCache";
}

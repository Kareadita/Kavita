namespace API.Constants;

public static class ResponseCacheProfiles
{
    public const string Images = "Images";
    public const string Hour = "Hour";
    public const string TenMinute = "10Minute";
    public const string FiveMinute = "5Minute";
    /// <summary>
    /// 6 hour long cache as underlying API is expensive
    /// </summary>
    public const string Statistics = "Statistics";
    /// <summary>
    /// Instant is a very quick cache, because we can't bust based on the query params, but rather body
    /// </summary>
    public const string Instant = "Instant";
    public const string Month = "Month";
    public const string LicenseCache = "LicenseCache";
    public const string KavitaPlus = "KavitaPlus";
}

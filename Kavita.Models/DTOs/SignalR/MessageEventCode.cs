namespace Kavita.Models.DTOs.SignalR;

/// <summary>
/// Stable keys set on <see cref="SignalRMessage.Code"/>. The UI translates these, so never rename a value
/// </summary>
public static class MessageEventCode
{
    #region Scan Loop

    public const string ScanListingFolders = "scan-listing-folders";
    public const string ScanReadingFiles = "scan-reading-files";
    public const string ScanGroupingSeries = "scan-grouping-series";
    public const string ScanProcessingSeries = "scan-processing-series";

    #endregion
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kavita.Models.DTOs.KavitaPlus.License;

namespace Kavita.Models.DebugExport;
#nullable enable

/// <summary>
/// The <c>system-info.json</c> entry of a debug export
/// </summary>
public sealed record ExportSystemInfo(
    ExportSystemInfo.ExportSection Export,
    ExportSystemInfo.RuntimeSection Runtime,
    ExportSystemInfo.ConfigSection Config,
    IReadOnlyList<ExportSystemInfo.LibrarySection> Libraries,
    ExportSystemInfo.KavitaPlusSection KavitaPlus)
{
    public const string Unavailable = "unavailable";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public sealed record ExportSection(DateTime CreatedUtc, string KavitaVersion, string AdminUserName, string AdminPassword, string AdminAuthKey);

    public sealed record RuntimeSection(string Os, string Architecture, string Framework, string Rid, bool InContainer, int Cores, string Locale);

    public sealed record ConfigSection(string Path, MountSection? Mount, WriteTimesSection WriteTimes);

    /// <param name="Source">Host replaced with the junk the database and logs use for it</param>
    /// <param name="Fingerprint">The salted value the scrubbed <c>Library.FolderWatchState</c> holds for this mount. Null for <c>config/</c></param>
    public sealed record MountSection(string MountPoint, string Source, string Type, bool IsNetwork, string? Fingerprint);

    public sealed record WriteTimesSection(int Rounds, int BytesEach, double? MedianMs, double? MaxMs, string? Error);

    public sealed record LibrarySection(int Id, IReadOnlyList<FolderSection> Folders, IReadOnlyList<ReadTimeSection> ReadTimes);

    /// <param name="Mount">Null when the mount cannot be read</param>
    public sealed record FolderSection(string Path, MountSection? Mount);

    /// <param name="File">The <c>MangaFile</c> id</param>
    public sealed record ReadTimeSection(int File, long? Bytes, long? FirstMs, long? SecondMs, string? Error);

    /// <param name="License">A <see cref="LicenseSection"/>, <see cref="Unavailable"/> when K+ gave nothing back, null with no license key</param>
    public sealed record KavitaPlusSection(bool HasLicense, object? License, string SupportToken);

    public sealed record LicenseSection(KavitaPlusSubscriptionState State, bool IsActive, DateTime ExpirationDate, bool PastDue, bool IsValidVersion);
}

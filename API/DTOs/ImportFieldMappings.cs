using System.Collections.Generic;
using System.ComponentModel;
using API.DTOs.KavitaPlus.Metadata;

namespace API.DTOs;

/// <summary>
/// How Kavita should import the new settings
/// </summary>
public enum ImportMode
{
    [Description("Replace")]
    Replace = 0,
    [Description("Merge")]
    Merge = 1,
}

/// <summary>
/// How Kavita should resolve conflicts
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Require the user to override the default
    /// </summary>
    [Description("Manual")]
    Manual = 0,
    /// <summary>
    /// Keep current value
    /// </summary>
    [Description("Keep")]
    Keep = 1,
    /// <summary>
    /// Replace with imported value
    /// </summary>
    [Description("Replace")]
    Replace = 2,
}

public sealed record ImportSettingsDto
{
    /// <summary>
    /// How Kavita should import the new settings
    /// </summary>
    public ImportMode ImportMode { get; init; }
    /// <summary>
    /// Default conflict resolution, override with <see cref="AgeRatingConflictResolutions"/> and <see cref="FieldMappingsConflictResolutions"/>
    /// </summary>
    public ConflictResolution Resolution { get; init; }
    /// <summary>
    /// Import <see cref="MetadataSettingsDto.Whitelist"/>
    /// </summary>
    public bool Whitelist { get; init; }
    /// <summary>
    /// Import <see cref="MetadataSettingsDto.Blacklist"/>
    /// </summary>
    public bool Blacklist { get; init; }
    /// <summary>
    /// Import <see cref="MetadataSettingsDto.AgeRatingMappings"/>
    /// </summary>
    public bool AgeRatings { get; init; }
    /// <summary>
    /// Import <see cref="MetadataSettingsDto.FieldMappings"/>
    /// </summary>
    public bool FieldMappings  { get; init; }

    /// <summary>
    /// Override the <see cref="Resolution"/> for specific age ratings
    /// </summary>
    /// <remarks>Key is the tag</remarks>
    public Dictionary<string, ConflictResolution> AgeRatingConflictResolutions { get; init; }
}

public sealed record FieldMappingsImportResultDto
{
    public bool Success { get; init; }
    /// <summary>
    /// Only present if <see cref="Success"/> is true
    /// </summary>
    public MetadataSettingsDto ResultingMetadataSettings { get; init; }
    /// <summary>
    /// Keys of the conflicting age ratings mappings
    /// </summary>
    public List<string> AgeRatingConflicts { get; init; }
}

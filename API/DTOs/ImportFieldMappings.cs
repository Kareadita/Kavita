using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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

public enum ConflictResolution
{
    [Description("Manual")]
    Manual = 0,
    [Description("Keep")]
    Keep = 1,
    [Description("Replace")]
    Replace = 2,
}

public sealed record ImportSettingsDto
{
    public ImportMode ImportMode { get; init; }
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
    /// <summary>
    /// Override the <see cref="Resolution"/> for specific field mappings
    /// </summary>
    /// <remarks>Key is the id in the database</remarks>
    public Dictionary<int, ConflictResolution> FieldMappingsConflictResolutions  { get; init; }
}

public sealed record ImportConflict
{
    /// <summary>
    /// The id of the entity in DB
    /// </summary>
    public int OldId { get; init; }
    /// <summary>
    /// The id of the enity form the imported json
    /// </summary>
    public int NewId { get; init; }
}

public sealed record FieldMappingsImportResultDto
{
    public bool Success { get; init; }
    /// <summary>
    /// Only present if <see cref="Success"/> is true
    /// </summary>
    public MetadataSettingsDto ResultingMetadataSettings { get; init; }
    public List<string> AgeRatingConflicts { get; init; }
    public List<ImportConflict> FieldMappingConflicts { get; init; }
}

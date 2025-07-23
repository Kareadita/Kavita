using System.Collections.Generic;

namespace API.DTOs;

public enum ImportMode
{
    Replace = 0,
    Merge = 1,
}

public enum ConflictResolution
{
    Manual = 0,
    Keep = 1,
    Replace = 2,
}

public sealed record ImportSettingsDto
{
    public ImportMode ImportMode { get; init; }
    public ConflictResolution Resolution { get; init; }
    public bool Whitelist { get; init; }
    public bool Blacklist { get; init; }
    public bool AgeRatings { get; init; }
    public bool FieldMappings  { get; init; }

    public Dictionary<string, ConflictResolution> AgeRatingConflictResolutions { get; init; }
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
    public List<string> AgeRatingConflicts { get; init; }
    public List<ImportConflict> FieldMappingConflicts { get; init; }
}

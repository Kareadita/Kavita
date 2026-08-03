using System.Collections.Generic;

namespace Kavita.Models.DTOs.Metadata;

public sealed record RunMetadataMappingsRequestDto
{

    /// <summary>
    /// When true <see cref="IncludedLibraries"/> is ignored
    /// </summary>
    public bool AllLibraries {  get; init; }

    /// <summary>
    /// Libraries to process. Ignored if <see cref="AllLibraries"/> is true
    /// </summary>
    public List<int> IncludedLibraries {  get; init; }

    /// <summary>
    /// Libraries to skip, can be used to request all - 1 libraries
    /// </summary>
    public List<int> ExcludedLibraries {  get; init; }
}

using System.Collections.Generic;

namespace Kavita.Models.DTOs.Metadata;

public sealed record ReRunMappingsRequest
{

    /// <summary>
    /// When true <see cref="IncludedLibraries"/> is ignored
    /// </summary>
    public bool AllLibraries {  get; set; }

    /// <summary>
    /// Libraries to process. Ignored if <see cref="AllLibraries"/> is true
    /// </summary>
    public List<int> IncludedLibraries {  get; set; }

    /// <summary>
    /// Libraries to skip, can be used to request all - 1 libraries
    /// </summary>
    public List<int> ExcludedLibraries {  get; set; }

}

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Kavita.Models.DTOs.Metadata;

public sealed record ReRunMappingsRequest: IValidatableObject
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AllLibraries && IncludedLibraries?.Count == 0)
        {
            yield return new ValidationResult(
                "You must either select 'All Libraries' or specify at least one included library.",
                [nameof(AllLibraries), nameof(IncludedLibraries)]
            );
        }

        if (ExcludedLibraries?.Intersect(IncludedLibraries ?? []).Count() > 0)
        {
            yield return new ValidationResult(
                $"{nameof(ExcludedLibraries)} and {nameof(IncludedLibraries)} cannot intersect",
                [nameof(ExcludedLibraries), nameof(IncludedLibraries)]);
        }
    }
}

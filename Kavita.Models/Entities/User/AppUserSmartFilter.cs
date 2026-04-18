using Kavita.Models.DTOs.Filtering.v2;

namespace Kavita.Models.Entities.User;

/// <summary>
/// Represents a Saved user Filter
/// </summary>
public class AppUserSmartFilter
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// This is the Filter url encoded. It is decoded and reconstructed into a <see cref="IFilterDto"/>
    /// </summary>
    public required string Filter { get; set; }
    public required FilterEntityType EntityType { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }
}

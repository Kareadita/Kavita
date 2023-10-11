namespace API.Entities;

public class AppUserSideNavStream
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// Is System Provided
    /// </summary>
    public bool IsProvided { get; set; }
    /// <summary>
    /// Sort Order on the Dashboard
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Library Id is for StreamType.Library only
    /// </summary>
    public int? LibraryId { get; set; }
    /// <summary>
    /// Only set for StreamType.ExternalSource
    /// </summary>
    public int? ExternalSourceId { get; set; }
    /// <summary>
    /// For system provided
    /// </summary>
    public SideNavStreamType StreamType { get; set; }
    public bool Visible { get; set; }
    /// <summary>
    /// If Not IsProvided, the appropriate smart filter
    /// </summary>
    public AppUserSmartFilter? SmartFilter { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }
}

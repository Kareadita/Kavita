namespace Kavita.Models.DTOs.Settings;

public enum KepubifySource
{
    /// <summary>
    /// The binary that ships beside Kavita, checked against a hash list
    /// </summary>
    Shipped = 0,
    /// <summary>
    /// The binary named by KepubifyPath in appsettings.json. Not hashed
    /// </summary>
    Custom = 1,
}

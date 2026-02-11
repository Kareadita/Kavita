using Kavita.Models.Entities.Enums.Theme;

namespace Kavita.Models.Entities.Interfaces;

/// <summary>
/// A theme in some kind
/// </summary>
public interface ITheme
{
    public string Name { get; set; }
    public string NormalizedName { get; set; }
    public string FileName { get; set; }
    public bool IsDefault { get; set; }
    public ThemeProvider Provider { get; set; }
}

using Kavita.Models.Entities.Enums.Font;

namespace Kavita.Models.DTOs.Font;

public sealed record EpubFontDto
{
    public int Id { get; set; }
    public string Family { get; set; }
    public string Name { get; set; }
    public FontProvider Provider { get; set; }
    public string FileName { get; set; }
    public string Style { get; set; }
    public string Weight { get; set; }

}

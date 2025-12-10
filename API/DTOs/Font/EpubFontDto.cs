using API.Entities.Enums.Font;

namespace API.DTOs.Font;

public sealed record EpubFontDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public FontProvider Provider { get; set; }
    public string FileName { get; set; }

}

using System;
using API.Entities.Enums.Font;

namespace API.DTOs.Font;

public class EpubFontDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public FontProvider Provider { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
}

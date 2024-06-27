using CsvHelper.Configuration.Attributes;

namespace API.DTOs.Stats;

/// <summary>
/// Excel export for File Extension Report
/// </summary>
public class FileExtensionExportDto
{
    [Name("Path")]
    public string FilePath { get; set; }

    [Name("Extension")]
    public string Extension { get; set; }
}

using System.ComponentModel;

namespace Kavita.Models.Entities.Enums.UserPreferences;

public enum PdfSpreadMode
{
    [Description("None")]
    None = 0,
    [Description("Odd")]
    Odd = 1,
    [Description("Even")]
    Even = 2
}

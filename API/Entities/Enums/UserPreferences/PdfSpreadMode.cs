using System.ComponentModel;

namespace API.Entities.Enums.UserPreferences;

public enum PdfSpreadMode
{
    [Description("None")]
    None = 0,
    [Description("Odd")]
    Odd = 1,
    [Description("Even")]
    Even = 2
}

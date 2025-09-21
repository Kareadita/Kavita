using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Due to a bleeding text bug in the Epub reader with 1/2 column layout, multiple calculation modes are present
/// </summary>
public enum EpubPageCalculationMethod
{
    [Description("Default")]
    Default = 0,
    [Description("Calculation 1")]
    Calculation1 = 1,
}

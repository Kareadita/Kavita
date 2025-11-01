using System.Collections.Generic;

namespace API.Entities.Enums.UserPreferences;
#nullable enable

public sealed record KeyBind
{
    public string Key { get; set; }
    public bool Control { get; set; }
    public bool Shift { get; set; }
    public bool Meta { get; set; }
    public bool Alt { get; set; }
    public IList<string>? ControllerSequence { get; set; }
}

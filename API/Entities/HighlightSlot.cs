namespace API.Entities;

public sealed record HighlightSlot
{
    public int Id { get; set; }
    public int SlotNumber { get; set; }
    public RgbaColor Color { get; set; }
}

public struct RgbaColor
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
    public float A { get; set; }
}

namespace API.Entities.Enums;

public enum CoverImageSize
{
    /// <summary>
    /// Default Size: 320x455 (wxh)
    /// </summary>
    Default = 1,
    /// <summary>
    /// 640x909
    /// </summary>
    Medium = 2,
    /// <summary>
    /// 900x1277
    /// </summary>
    Large = 3,
    /// <summary>
    /// 1265x1795
    /// </summary>
    XLarge = 4
}

public static class CoverImageSizeExtensions
{
    public static (int Width, int Height) GetDimensions(this CoverImageSize size)
    {
        return size switch
        {
            CoverImageSize.Default => (320, 455),
            CoverImageSize.Medium => (640, 909),
            CoverImageSize.Large => (900, 1277),
            CoverImageSize.XLarge => (1265, 1795),
            _ => (320, 455)
        };
    }
}

namespace API.Entities.Enums;

public enum PdfRenderResolution
{
    /// <summary>
    /// Default Size: 1080x1920 (wxh)
    /// </summary>
    Default = 1,
    /// <summary>
    /// 1920x2560
    /// </summary>
    High = 2,
    /// <summary>
    /// 2160x3840
    /// </summary>
    Ultra = 3,
}
public static class PdfRenderResolutionExtensions
{
    public static (int dim1, int dim2) GetDimensions(this PdfRenderResolution size)
    {
        return size switch
        {
            PdfRenderResolution.Default => (1080, 1920),
            PdfRenderResolution.High => (1920, 2560),
            PdfRenderResolution.Ultra => (2160, 3840),
            _ => (1080, 1920)
        };
    }
}

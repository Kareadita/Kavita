namespace API.Entities.Enums;

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

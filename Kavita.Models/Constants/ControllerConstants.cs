namespace Kavita.Models.Constants;

public abstract class ControllerConstants
{
    /// <summary>
    /// Max request body size for upload endpoints that carry raw bytes (multipart file upload) or a legacy base64
    /// payload. Cover selection from a URL/Kavita+ now streams the file into temp and only posts the temp filename,
    /// so it is not bound by this limit.
    /// </summary>
    public const int MaxUploadSizeBytes = 31_457_280; // 30 MB
}

namespace API.DTOs.Reader;

public sealed record BookResourceResultDto
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; }
    public byte[] Content { get; init; }
    public string ContentType { get; init; }
    public string FileName { get; init; }

    public static BookResourceResultDto Success(byte[] content, string contentType, string fileName) =>
        new() { IsSuccess = true, Content = content, ContentType = contentType, FileName = fileName };

    public static BookResourceResultDto Error(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}

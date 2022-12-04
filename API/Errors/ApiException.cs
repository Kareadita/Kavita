namespace API.Errors;

public class ApiException
{
    private int Status { get; init; }
    private string? Message { get; init; }
    private string? Details { get; init; }

    public ApiException(int status, string? message = null, string? details = null)
    {
        Status = status;
        Message = message;
        Details = details;
    }
}

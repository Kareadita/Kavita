namespace API.Errors;

public class ApiException
{
    public int Status { get; init; }
    public string Message { get; init; }
    public string Details { get; init; }

    public ApiException(int status, string message = null, string details = null)
    {
        Status = status;
        Message = message;
        Details = details;
    }
}

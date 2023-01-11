namespace API.Errors;

public record ApiException
{
    public ApiException(int status, string? message = null, string? details = null)
    {

    }
}

namespace API.Errors;

#nullable enable
public record ApiException(int Status, string? Message = null, string? Details = null);

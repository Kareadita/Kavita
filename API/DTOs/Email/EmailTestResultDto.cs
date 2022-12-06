namespace API.DTOs.Email;

/// <summary>
/// Represents if Test Email Service URL was successful or not and if any error occured
/// </summary>
public class EmailTestResultDto
{
    public bool Successful { get; set; }
    public string ErrorMessage { get; set; } = default!;
}

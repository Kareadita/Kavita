namespace API.DTOs.Account;

public class UpdateEmailResponse
{
    /// <summary>
    /// Did the user not have an existing email
    /// </summary>
    /// <remarks>This informs the user to check the new email address</remarks>
    public bool HadNoExistingEmail { get; set; }
    /// <summary>
    /// Was an email sent (ie is this server accessible)
    /// </summary>
    public bool EmailSent { get; set; }
}

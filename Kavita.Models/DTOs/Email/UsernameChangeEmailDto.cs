namespace Kavita.Models.DTOs.Email;

public sealed record UsernameChangeEmailDto
{
    /// <summary>
    /// User Id to resolve the locale against
    /// </summary>
    public required int LocaleUserId { get; init; }
    public string EmailAddress { get; init; }
    public string InvitingUser { get; init; }
}

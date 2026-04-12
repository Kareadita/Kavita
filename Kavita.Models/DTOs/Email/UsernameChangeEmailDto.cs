namespace Kavita.Models.DTOs.Email;

public sealed record UsernameChangeEmailDto
{
    /// <summary>
    /// User Id to resolve the locale against
    /// </summary>
    public required int LocaleUserId { get; init; }
    public required string EmailAddress { get; init; }
    public required string InvitingUser { get; init; }
}

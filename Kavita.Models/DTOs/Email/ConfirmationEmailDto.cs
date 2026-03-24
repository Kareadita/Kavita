namespace Kavita.Models.DTOs.Email;

public sealed record ConfirmationEmailDto
{
    public string InvitingUser { get; init; } = default!;
    /// <summary>
    /// User Id to resolve the locale against
    /// </summary>
    public required int LocaleUserId { get; init; }
    public string EmailAddress { get; init; } = default!;
    public string ServerConfirmationLink { get; init; } = default!;
    /// <summary>
    /// InstallId of this Kavita Instance
    /// </summary>
    public string InstallId { get; init; } = default!;
}

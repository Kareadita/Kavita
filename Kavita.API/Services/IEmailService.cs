using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Email;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Http;

namespace Kavita.API.Services;

public interface IEmailService
{
    Task SendInviteEmail(ConfirmationEmailDto data);
    Task<bool> SendForgotPasswordEmail(PasswordResetEmailDto dto);
    Task<bool> SendFilesToEmail(SendToDto data);
    Task<EmailTestResultDto> SendTestEmail(string adminEmail);
    Task SendEmailChangeEmail(ConfirmationEmailDto data);
    Task SendUsernameChangeEmail(UsernameChangeEmailDto data);
    bool IsValidEmail(string? email);

    Task<string> GenerateEmailLink(HttpRequest request, string token, string routePart, string email,
        bool withHost = true);

    Task<bool> SendTokenExpiredEmail(int userId, ScrobbleProvider provider);
    Task<bool> SendTokenExpiringSoonEmail(int userId, ScrobbleProvider provider);
    Task<bool> SendAuthKeyExpiredEmail(int userId, IList<AppUserAuthKey> keys);
    Task<bool> SendAuthKeyExpiringSoonEmail(int userId, IList<AppUserAuthKey> keys);
    Task<bool> SendKavitaPlusDebug();
}

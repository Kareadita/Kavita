using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using API.Data;
using API.DTOs.Account;
using API.DTOs.Email;
using API.Entities;
using API.Entities.User;
using API.Services.Plus;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeTypes;

namespace API.Services;
#nullable enable

internal class EmailOptionsDto
{
    public required IList<string> ToEmails { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public required string Preheader { get; set; }
    public IList<KeyValuePair<string, string>>? PlaceHolders { get; set; }
    /// <summary>
    /// Filenames to attach
    /// </summary>
    public IList<string>? Attachments { get; set; }
    public int? ToUserId { get; set; }
    public required string Template { get; set; }
}

public interface IEmailService
{
    Task SendInviteEmail(ConfirmationEmailDto data);
    Task<bool> SendForgotPasswordEmail(PasswordResetEmailDto dto);
    Task<bool> SendFilesToEmail(SendToDto data);
    Task<EmailTestResultDto> SendTestEmail(string adminEmail);
    Task SendEmailChangeEmail(ConfirmationEmailDto data);
    bool IsValidEmail(string email);

    Task<string> GenerateEmailLink(HttpRequest request, string token, string routePart, string email,
        bool withHost = true);

    Task<bool> SendTokenExpiredEmail(int userId, ScrobbleProvider provider);
    Task<bool> SendTokenExpiringSoonEmail(int userId, ScrobbleProvider provider);
    Task<bool> SendAuthKeyExpiredEmail(int userId, IList<AppUserAuthKey> keys);
    Task<bool> SendAuthKeyExpiringSoonEmail(int userId, IList<AppUserAuthKey> keys);
    Task<bool> SendKavitaPlusDebug();
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IHostEnvironment _environment;
    private readonly ILocalizationService _localizationService;

    private const string TemplatePath = @"{0}.html";
    private const string LocalHost = "localhost:4200";

    public const string SendToDeviceTemplate = "SendToDevice";
    public const string EmailTestTemplate = "EmailTest";
    public const string EmailChangeTemplate = "EmailChange";
    public const string TokenExpirationTemplate = "TokenExpiration";
    public const string TokenExpiringSoonTemplate = "TokenExpiringSoon";
    public const string AuthKeyExpiredTemplate = "AuthKeyExpired";
    public const string AuthKeyExpiringSoonTemplate = "AuthKeyExpiringSoon";
    public const string EmailConfirmTemplate = "EmailConfirm";
    public const string EmailPasswordResetTemplate = "EmailPasswordReset";
    public const string KavitaPlusDebugTemplate = "KavitaPlusDebug";

    private const string AuthKeyExpiringFragment = "AuthKeyExpiringFragment";
    private const string AuthKeyExpiredFragment = "AuthKeyExpiredFragment";

    public EmailService(ILogger<EmailService> logger, IUnitOfWork unitOfWork, IDirectoryService directoryService,
        IHostEnvironment environment, ILocalizationService localizationService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _environment = environment;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Test if the email settings are working. Rejects if user email isn't valid or not all data is setup in server settings.
    /// </summary>
    /// <returns></returns>
    public async Task<EmailTestResultDto> SendTestEmail(string adminEmail)
    {
        var result = new EmailTestResultDto
        {
            EmailAddress = adminEmail
        };

        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (!IsValidEmail(adminEmail))
        {
            var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser();
            result.ErrorMessage = await _localizationService.Translate(defaultAdmin.Id, "account-email-invalid");
            result.Successful = false;
            return result;
        }

        if (!settings.IsEmailSetup())
        {
            var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser();
            result.ErrorMessage = await _localizationService.Translate(defaultAdmin.Id, "email-settings-invalid");
            result.Successful = false;
            return result;
        }

        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{Host}}", settings.HostName),
        };

        try
        {
            var emailOptions = new EmailOptionsDto()
            {
                Subject = "Kavita - Email Test",
                Template = EmailTestTemplate,
                Body = UpdatePlaceHolders(await GetEmailBody(EmailTestTemplate), placeholders),
                Preheader = "Kavita - Email Test",
                ToEmails = new List<string>()
                {
                    adminEmail
                },
            };

            await SendEmail(emailOptions);
            result.Successful = true;
        }
        catch (KavitaException ex)
        {
            result.Successful = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Sends an email that has a link that will finalize an Email Change
    /// </summary>
    /// <param name="data"></param>
    public async Task SendEmailChangeEmail(ConfirmationEmailDto data)
    {
        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{InvitingUser}}", data.InvitingUser),
            new ("{{Link}}", data.ServerConfirmationLink)
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("Your email has been changed on {{InvitingUser}}'s Server", placeholders),
            Template = EmailChangeTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(EmailChangeTemplate), placeholders),
            Preheader = UpdatePlaceHolders("Your email has been changed on {{InvitingUser}}'s Server", placeholders),
            ToEmails = new List<string>()
            {
                data.EmailAddress
            }
        };

        await SendEmail(emailOptions);
    }

    /// <summary>
    /// Validates the email address. Does not test it actually receives mail
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    public bool IsValidEmail(string? email)
    {
        return !string.IsNullOrEmpty(email) && new EmailAddressAttribute().IsValid(email);
    }

    public async Task<string> GenerateEmailLink(HttpRequest request, string token, string routePart, string email, bool withHost = true)
    {
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var host = _environment.IsDevelopment() ? LocalHost : request.Host.ToString();
        var basePart = $"{request.Scheme}://{host}{request.PathBase}";
        if (!string.IsNullOrEmpty(serverSettings.HostName))
        {
            basePart = serverSettings.HostName;
            if (!serverSettings.BaseUrl.Equals(Configuration.DefaultBaseUrl))
            {
                var removeCount = serverSettings.BaseUrl.EndsWith('/') ? 1 : 0;
                basePart += serverSettings.BaseUrl[..^removeCount];
            }
        }

        if (withHost) return $"{basePart}/registration/{routePart}?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(email)}";
        return $"registration/{routePart}?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(email)}"
            .Replace("//", "/");
    }

    public async Task<bool> SendTokenExpiredEmail(int userId, ScrobbleProvider provider)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (user == null || !IsValidEmail(user.Email) || !settings.IsEmailSetup()) return false;

        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{UserName}}", user.UserName!),
            new ("{{Provider}}", provider.ToDescription()),
            new ("{{Link}}", $"{settings.HostName}/settings#account" ),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("Kavita - Your {{Provider}} token has expired and scrobbling events have stopped", placeholders),
            Template = TokenExpirationTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(TokenExpirationTemplate), placeholders),
            Preheader = UpdatePlaceHolders("Kavita - Your {{Provider}} token has expired and scrobbling events have stopped", placeholders),
            ToEmails = new List<string>()
            {
                user.Email
            }
        };

        await SendEmail(emailOptions);

        return true;
    }

    public async Task<bool> SendTokenExpiringSoonEmail(int userId, ScrobbleProvider provider)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (user == null || !IsValidEmail(user.Email) || !settings.IsEmailSetup()) return false;

        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{UserName}}", user.UserName!),
            new ("{{Provider}}", provider.ToDescription()),
            new ("{{Link}}", $"{settings.HostName}/settings#account" ),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("Kavita - Your {{Provider}} token will expire soon!", placeholders),
            Template = TokenExpiringSoonTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(TokenExpiringSoonTemplate), placeholders),
            Preheader = UpdatePlaceHolders("Kavita - Your {{Provider}} token will expire soon!", placeholders),
            ToEmails = new List<string>()
            {
                user.Email
            }
        };

        await SendEmail(emailOptions);

        return true;
    }

    public async Task<bool> SendAuthKeyExpiredEmail(int userId, IList<AppUserAuthKey> keys)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (user == null || !IsValidEmail(user.Email) || !settings.IsEmailSetup()) return false;

        var d = keys.Select(k => new List<KeyValuePair<string, string>>()
        {
            new("{{Name}}", k.Name),
        });

        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{UserName}}", user.UserName!),
            new ("{{AuthKeyFragment}}", await BuildFragment(AuthKeyExpiredFragment, d)),
            new ("{{Link}}", $"{settings.HostName}/settings#account" ),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = "Kavita - One or more Auth Keys has expired!",
            Template = AuthKeyExpiredTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(TokenExpirationTemplate), placeholders),
            Preheader = "Kavita - One or more Auth Keys has expired!",
            ToEmails = new List<string>()
            {
                user.Email
            }
        };

        await SendEmail(emailOptions);

        return true;
    }


    public async Task<bool> SendAuthKeyExpiringSoonEmail(int userId, IList<AppUserAuthKey> keys)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (user == null || !IsValidEmail(user.Email) || !settings.IsEmailSetup()) return false;

        var d = keys.Select(k => new List<KeyValuePair<string, string>>()
        {
            new("{{Name}}", k.Name),
            new("{{DaysLeft}}", (k.ExpiresAtUtc.Value - DateTime.UtcNow).Days.ToString())
        });


        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{UserName}}", user.UserName!),
            new ("{{AuthKeyFragment}}", await BuildFragment(AuthKeyExpiringFragment, d)),
            new ("{{Link}}", $"{settings.HostName}/settings#clients" ),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = "Kavita - One or more Auth Keys will expire soon!",
            Template = AuthKeyExpiringSoonTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(AuthKeyExpiringSoonTemplate), placeholders),
            Preheader = "Kavita - One or more Auth Keys will expire soon!",
            ToEmails = new List<string>()
            {
                user.Email
            }
        };

        await SendEmail(emailOptions);

        return true;
    }

    /// <summary>
    /// Sends information about Kavita install for Kavita+ registration
    /// </summary>
    /// <example>Users in China can have issues subscribing, this flow will allow me to register their instance on their behalf</example>
    /// <returns></returns>
    public async Task<bool> SendKavitaPlusDebug()
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (!settings.IsEmailSetup()) return false;

        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{InstallId}}", HashUtil.ServerToken()),
            new ("{{Build}}", BuildInfo.Version.ToString()),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("Kavita+: A User needs manual registration", placeholders),
            Template = KavitaPlusDebugTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(KavitaPlusDebugTemplate), placeholders),
            Preheader = UpdatePlaceHolders("Kavita+: A User needs manual registration", placeholders),
            ToEmails =
            [
                // My kavita email
                Encoding.UTF8.GetString(Convert.FromBase64String("a2F2aXRhcmVhZGVyQGdtYWlsLmNvbQ=="))
            ]
        };

        await SendEmail(emailOptions);

        return true;
    }

    /// <summary>
    /// Sends an invite email to a user to setup their account
    /// </summary>
    /// <param name="data"></param>
    public async Task SendInviteEmail(ConfirmationEmailDto data)
    {
        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{InvitingUser}}", data.InvitingUser),
            new ("{{Link}}", data.ServerConfirmationLink)
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("You've been invited to join {{InvitingUser}}'s Kavita Server", placeholders),
            Template = EmailConfirmTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(EmailConfirmTemplate), placeholders),
            Preheader = UpdatePlaceHolders("You've been invited to join {{InvitingUser}}'s Kavita Server", placeholders),
            ToEmails = new List<string>()
            {
                data.EmailAddress
            }
        };

        await SendEmail(emailOptions);
    }


    public async Task<bool> SendForgotPasswordEmail(PasswordResetEmailDto dto)
    {
        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{Link}}", dto.ServerConfirmationLink),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("A password reset has been requested", placeholders),
            Template = EmailPasswordResetTemplate,
            Body = UpdatePlaceHolders(await GetEmailBody(EmailPasswordResetTemplate), placeholders),
            Preheader = "Email confirmation is required for continued access. Click the button to confirm your email.",
            ToEmails =
            [
                dto.EmailAddress
            ]
        };

        await SendEmail(emailOptions);
        return true;
    }

    public async Task<bool> SendFilesToEmail(SendToDto data)
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (!serverSetting.IsEmailSetupForSendToDevice()) return false;

        var emailOptions = new EmailOptionsDto()
        {
            Subject = "Send file from Kavita",
            Preheader = "File(s) sent from Kavita",
            ToEmails = [data.DestinationEmail],
            Template = SendToDeviceTemplate,
            Body = await GetEmailBody(SendToDeviceTemplate),
            Attachments = data.FilePaths.ToList()
        };

        await SendEmail(emailOptions);
        return true;
    }

    private async Task SendEmail(EmailOptionsDto userEmailOptions)
    {
        var smtpConfig = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).SmtpConfig;
        var email = new MimeMessage()
        {
            Subject = userEmailOptions.Subject,
        };
        email.From.Add(new MailboxAddress(smtpConfig.SenderDisplayName, smtpConfig.SenderAddress));

        // Inject the body into the base template
        var fullBody = UpdatePlaceHolders(await GetEmailBody("base"), new List<KeyValuePair<string, string>>()
        {
            new ("{{Body}}", userEmailOptions.Body),
            new ("{{Preheader}}", userEmailOptions.Preheader),
        });

        var body = new BodyBuilder
        {
            HtmlBody = fullBody
        };

        if (userEmailOptions.Attachments != null)
        {
            foreach (var attachmentPath in userEmailOptions.Attachments)
            {
                var mimeType = MimeTypeMap.GetMimeType(attachmentPath) ?? "application/octet-stream";
                var mediaType = mimeType.Split('/')[0];
                var mediaSubtype = mimeType.Split('/')[1];

                var attachment = new MimePart(mediaType, mediaSubtype)
                {
                    Content = new MimeContent(File.OpenRead(attachmentPath)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = Path.GetFileName(attachmentPath)
                };

                body.Attachments.Add(attachment);
            }
        }

        email.Body = body.ToMessageBody();

        foreach (var toEmail in userEmailOptions.ToEmails)
        {
            email.To.Add(new MailboxAddress(toEmail, toEmail));
        }

        using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
        smtpClient.Timeout = 20000;
        var ssl = smtpConfig.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;



        ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;

        var emailAddress = userEmailOptions.ToEmails[0];
        AppUser? user;
        if (userEmailOptions.Template == SendToDeviceTemplate)
        {
            user = await _unitOfWork.UserRepository.GetUserByDeviceEmail(emailAddress);
        }
        else
        {
            user = await _unitOfWork.UserRepository.GetUserByEmailAsync(emailAddress);
        }


        try
        {
            await smtpClient.ConnectAsync(smtpConfig.Host, smtpConfig.Port, ssl);
            if (!string.IsNullOrEmpty(smtpConfig.UserName) && !string.IsNullOrEmpty(smtpConfig.Password))
            {
                await smtpClient.AuthenticateAsync(smtpConfig.UserName, smtpConfig.Password);
            }

            await smtpClient.SendAsync(email);
            if (user != null)
            {
                await LogEmailHistory(user.Id, userEmailOptions.Template, userEmailOptions.Subject, userEmailOptions.Body, "Sent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue sending the email");

            if (user != null)
            {
                await LogEmailHistory(user.Id, userEmailOptions.Template, userEmailOptions.Subject, userEmailOptions.Body, "Failed", ex.Message);
            }
            _logger.LogError("Could not find user on file for email, {Template} email was not sent and not recorded into history table", userEmailOptions.Template);

            throw;
        }
        finally
        {
            await smtpClient.DisconnectAsync(true);

        }
    }

    /// <summary>
    /// Logs email history for the specified user.
    /// </summary>
    private async Task LogEmailHistory(int appUserId, string emailTemplate, string subject, string body, string deliveryStatus, string? errorMessage = null)
    {
        var emailHistory = new EmailHistory
        {
            AppUserId = appUserId,
            EmailTemplate = emailTemplate,
            Sent = deliveryStatus == "Sent",
            Body = body,
            Subject = subject,
            SendDate = DateTime.UtcNow,
            DeliveryStatus = deliveryStatus,
            ErrorMessage = errorMessage
        };

        _unitOfWork.DataContext.EmailHistory.Add(emailHistory);
        await _unitOfWork.CommitAsync();
    }

    private async Task<string> GetTemplatePath(string templateName)
    {
        if ((await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).SmtpConfig.CustomizedTemplates)
        {
            var templateDirectory = Path.Join(_directoryService.CustomizedTemplateDirectory, TemplatePath);
            var fullName = string.Format(templateDirectory, templateName);
            if (_directoryService.FileSystem.File.Exists(fullName)) return fullName;
            _logger.LogError("Customized Templates is on, but template {TemplatePath} is missing", fullName);
        }

        return string.Format(Path.Join(_directoryService.TemplateDirectory, TemplatePath), templateName);
    }

    private async Task<string> GetEmailBody(string templateName)
    {
        var templatePath = await GetTemplatePath(templateName);

        return await File.ReadAllTextAsync(templatePath);
    }

    private static string UpdatePlaceHolders(string text, IList<KeyValuePair<string, string>>? keyValuePairs)
    {
        if (string.IsNullOrEmpty(text) || keyValuePairs == null) return text;

        foreach (var (key, value) in keyValuePairs)
        {
            if (text.Contains(key))
            {
                text = text.Replace(key, value);
            }
        }

        return text;
    }

    private async Task<string> BuildFragment(string fragment, IEnumerable<IList<KeyValuePair<string, string>>> placeholders)
    {
        StringBuilder builder = new();
        foreach (var placeholder in placeholders)
        {
            builder.Append(UpdatePlaceHolders(await GetEmailBody(fragment), placeholder));
        }

        return builder.ToString();
    }
}

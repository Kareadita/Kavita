using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using API.Data;
using API.DTOs.Email;
using Kavita.Common;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace API.Services;
#nullable enable

internal class EmailOptionsDto
{
    public IList<string> ToEmails { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public IList<KeyValuePair<string, string>> PlaceHolders { get; set; }
    /// <summary>
    /// Filenames to attach
    /// </summary>
    public IList<string>? Attachments { get; set; }
}

public interface IEmailService
{
    Task SendInviteEmail(ConfirmationEmailDto data);
    Task<bool> CheckIfAccessible(string host);
    Task<bool> SendForgotPasswordEmail(PasswordResetEmailDto dto);
    Task<bool> SendFilesToEmail(SendToDto data);
    Task<EmailTestResultDto> SendTestEmail(string adminEmail);
    Task SendEmailChangeEmail(ConfirmationEmailDto data);
    bool IsValidEmail(string email);

    Task<string> GenerateEmailLink(HttpRequest request, string token, string routePart, string email,
        bool withHost = true);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IHostEnvironment _environment;

    private const string TemplatePath = @"{0}.html";
    private const string LocalHost = "localhost:4200";

    public EmailService(ILogger<EmailService> logger, IUnitOfWork unitOfWork, IDirectoryService directoryService, IHostEnvironment environment)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _environment = environment;
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
        if (!IsValidEmail(adminEmail) || !settings.IsEmailSetup())
        {
            result.ErrorMessage = "You need to fill in more information in settings and ensure your account has a valid email to send a test email";
            result.Successful = false;
            return result;
        }

        // TODO: Come back and update the template. We can't do it with the v0.8.0 release
        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{Host}}", settings.HostName),
        };

        try
        {
            var emailOptions = new EmailOptionsDto()
            {
                Subject = "KavitaEmail Test",
                Body = UpdatePlaceHolders(await GetEmailBody("EmailTest"), placeholders),
                ToEmails = new List<string>()
                {
                    adminEmail
                }
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
            Body = UpdatePlaceHolders(await GetEmailBody("EmailChange"), placeholders),
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
    public bool IsValidEmail(string email)
    {
        return new EmailAddressAttribute().IsValid(email);
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
            Subject = UpdatePlaceHolders("You've been invited to join {{InvitingUser}}'s Server", placeholders),
            Body = UpdatePlaceHolders(await GetEmailBody("EmailConfirm"), placeholders),
            ToEmails = new List<string>()
            {
                data.EmailAddress
            }
        };

        await SendEmail(emailOptions);
    }

    public Task<bool> CheckIfAccessible(string host)
    {
        return Task.FromResult(true);
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
            Body = UpdatePlaceHolders(await GetEmailBody("EmailPasswordReset"), placeholders),
            ToEmails = new List<string>()
            {
                dto.EmailAddress
            }
        };

        await SendEmail(emailOptions);
        return true;
    }

    public async Task<bool> SendFilesToEmail(SendToDto data)
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (!serverSetting.IsEmailSetup()) return false;

        var emailOptions = new EmailOptionsDto()
        {
            Subject = "Send file from Kavita",
            ToEmails = new List<string>()
            {
                data.DestinationEmail
            },
            Body = await GetEmailBody("SendToDevice"),
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


        var body = new BodyBuilder
        {
            HtmlBody = userEmailOptions.Body
        };

        if (userEmailOptions.Attachments != null)
        {
            foreach (var attachment in userEmailOptions.Attachments)
            {
                await body.Attachments.AddAsync(attachment);
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

        await smtpClient.ConnectAsync(smtpConfig.Host, smtpConfig.Port, ssl);
        if (!string.IsNullOrEmpty(smtpConfig.UserName) && !string.IsNullOrEmpty(smtpConfig.Password))
        {
            await smtpClient.AuthenticateAsync(smtpConfig.UserName, smtpConfig.Password);
        }

        ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;

        try
        {
            await smtpClient.SendAsync(email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue sending the email");
            throw;
        }
        finally
        {
            await smtpClient.DisconnectAsync(true);
        }
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

        var body = await File.ReadAllTextAsync(templatePath);
        return body;
    }

    private static string UpdatePlaceHolders(string text, IList<KeyValuePair<string, string>> keyValuePairs)
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
}

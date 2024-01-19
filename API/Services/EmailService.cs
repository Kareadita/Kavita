using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Email;
using API.Entities.Enums;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
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
    Task<bool> SendMigrationEmail(EmailMigrationDto data);
    Task<bool> SendPasswordResetEmail(PasswordResetEmailDto data);
    Task<bool> SendFilesToEmail(SendToDto data);
    Task<EmailTestResultDto> SendTestEmail(string adminEmail);
    Task<bool> IsDefaultEmailService();
    Task SendEmailChangeEmail(ConfirmationEmailDto data);
    bool IsValidEmail(string email);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDownloadService _downloadService;
    private readonly IDirectoryService _directoryService;

    private const string TemplatePath = @"{0}.html";
    /// <summary>
    /// This is used to initially set or reset the ServerSettingKey. Do not access from the code, access via UnitOfWork
    /// </summary>
    public const string DefaultApiUrl = "https://email.kavitareader.com";


    public EmailService(ILogger<EmailService> logger, IUnitOfWork unitOfWork, IDownloadService downloadService, IDirectoryService directoryService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _downloadService = downloadService;
        _directoryService = directoryService;
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
                Body = UpdatePlaceHolders(GetEmailBody("EmailTest"), placeholders),
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


    [Obsolete]
    public async Task<bool> IsDefaultEmailService()
    {
        return (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EmailServiceUrl))!.Value!
            .Equals(DefaultApiUrl);
    }

    public async Task SendEmailChangeEmail(ConfirmationEmailDto data)
    {
        var emailLink = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EmailServiceUrl))!.Value;
        var success = await SendEmailWithPost(emailLink + "/api/account/email-change", data);
        if (!success)
        {
            _logger.LogError("There was a critical error sending Confirmation email");
        }
    }

    public bool IsValidEmail(string email)
    {
        return new EmailAddressAttribute().IsValid(email);
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
            Body = UpdatePlaceHolders(GetEmailBody("EmailConfirm"), placeholders),
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
        // // This is the only exception for using the default because we need an external service to check if the server is accessible for emails
        // try
        // {
        //     if (IsLocalIpAddress(host))
        //     {
        //         _logger.LogDebug("[EmailService] Server is not accessible, using local ip");
        //         return false;
        //     }
        //
        //     var url = DefaultApiUrl + "/api/reachable?host=" + host;
        //     _logger.LogDebug("[EmailService] Checking if this server is accessible for sending an email to: {Url}", url);
        //     return await SendEmailWithGet(url);
        // }
        // catch (Exception)
        // {
        //     return false;
        // }
    }

    public async Task<bool> SendMigrationEmail(EmailMigrationDto data)
    {
        var emailLink = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EmailServiceUrl)).Value;
        return await SendEmailWithPost(emailLink + "/api/invite/email-migration", data);
    }

    public async Task<bool> SendPasswordResetEmail(PasswordResetEmailDto dto)
    {
        // var emailLink = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EmailServiceUrl)).Value;
        // return await SendEmailWithPost(emailLink + "/api/invite/email-password-reset", data);

        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{Link}}", dto.ServerConfirmationLink),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("A password reset has been requested", placeholders),
            Body = UpdatePlaceHolders(GetEmailBody("EmailPasswordReset"), placeholders),
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
        if (serverSetting.IsEmailSetup()) return false;

        var emailOptions = new EmailOptionsDto()
        {
            Subject = "Send file from Kavita",
            ToEmails = new List<string>()
            {
                data.DestinationEmail
            },
            Body = GetEmailBody("SendToDevice"),
            Attachments = data.FilePaths.ToList()
        };

        await SendEmail(emailOptions);


        var emailLink = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EmailServiceUrl)).Value;
        return await SendEmailWithFiles(emailLink + "/api/sendto", data.FilePaths, data.DestinationEmail);

        // Check if Email is setup and confirmed (they will need to hit test)


    }

    private async Task<bool> SendEmailWithGet(string url, int timeoutSecs = 30)
    {
        try
        {
            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            var response = await (url)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("x-kavita-installId", settings.InstallId)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(timeoutSecs))
                .GetStringAsync();

            if (!string.IsNullOrEmpty(response) && bool.Parse(response))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            throw new KavitaException(ex.Message);
        }
        return false;
    }


    private async Task<bool> SendEmailWithPost(string url, object data, int timeoutSecs = 30)
    {
        try
        {
            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            var response = await (url)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("x-kavita-installId", settings.InstallId)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(timeoutSecs))
                .PostJsonAsync(data);

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                var errorMessage = await response.GetStringAsync();
                throw new KavitaException(errorMessage);
            }
        }
        catch (FlurlHttpException ex)
        {
            _logger.LogError(ex, "There was an exception when interacting with Email Service");
            return false;
        }
        return true;
    }


    private async Task<bool> SendEmailWithFiles(string url, IEnumerable<string> filePaths, string destEmail, int timeoutSecs = 300)
    {
        try
        {
            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            var response = await (url)
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("x-kavita-installId", settings.InstallId)
                .WithTimeout(timeoutSecs)
                .AllowHttpStatus("4xx")
                .PostMultipartAsync(mp =>
                {
                    mp.AddString("email", destEmail);
                    var index = 1;
                    foreach (var filepath in filePaths)
                    {
                        mp.AddFile("file" + index, filepath, _downloadService.GetContentTypeFromFile(filepath));
                        index++;
                    }
                }
                );

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                var errorMessage = await response.GetStringAsync();
                throw new KavitaException(errorMessage);
            }
        }
        catch (FlurlHttpException ex)
        {
            _logger.LogError(ex, "There was an exception when sending Email for SendTo");
            return false;
        }
        return true;
    }

    private static bool IsLocalIpAddress(string url)
    {
        var host = url.Split(':')[0];
        try
        {
            // get host IP addresses
            var hostIPs = Dns.GetHostAddresses(host);
            // get local IP addresses
            var localIPs = Dns.GetHostAddresses(Dns.GetHostName());

            // test if any host IP equals to any local IP or to localhost
            foreach (var hostIp in hostIPs)
            {
                // is localhost
                if (IPAddress.IsLoopback(hostIp)) return true;
                // is local address
                if (localIPs.Contains(hostIp))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignored
        }

        return false;
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

    private string GetEmailBody(string templateName)
    {
        var templateDirectory = Path.Join(_directoryService.TemplateDirectory, TemplatePath);
        var body = File.ReadAllText(string.Format(templateDirectory, templateName));
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

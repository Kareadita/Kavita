using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Email;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IEmailService
{
    Task SendConfirmationEmail(ConfirmationEmailDto data);
    Task<bool> CheckIfAccessible(string host);
    Task SendMigrationEmail(EmailMigrationDto data);
    Task SendPasswordResetEmail(PasswordResetEmailDto data);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private const string ApiUrl = "https://email.kavitareader.com";

    private const string TemplatePath = @"{0}.html";
    //private readonly SmtpConfig _smtpConfig;

    public EmailService(ILogger<EmailService> logger, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;

        FlurlHttp.ConfigureClient(ApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    public async Task SendConfirmationEmail(ConfirmationEmailDto data)
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

    public async Task<bool> CheckIfAccessible(string host)
    {
        return await SendEmailWithGet(ApiUrl + "/api/email/reachable?host=" + host);
    }

    public async Task SendMigrationEmail(EmailMigrationDto data)
    {
        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{Link}}", data.ServerConfirmationLink),
            new ("{{User}}", data.Username),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("Please validate your email to complete email migration", placeholders),
            Body = UpdatePlaceHolders(GetEmailBody("EmailMigration"), placeholders),
            ToEmails = new List<string>()
            {
                data.EmailAddress
            }
        };

        await SendEmail(emailOptions);
    }

    public async Task SendPasswordResetEmail(PasswordResetEmailDto data)
    {
        var placeholders = new List<KeyValuePair<string, string>>
        {
            new ("{{Link}}", data.ServerConfirmationLink),
        };

        var emailOptions = new EmailOptionsDto()
        {
            Subject = UpdatePlaceHolders("A password reset has been requested", placeholders),
            Body = UpdatePlaceHolders(GetEmailBody("EmailPasswordReset"), placeholders),
            ToEmails = new List<string>()
            {
                data.EmailAddress
            }
        };

        await SendEmail(emailOptions);
    }

    private static async Task<bool> SendEmailWithGet(string url)
    {
        try
        {
            var response = await (url)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(30))
                .GetStringAsync();

            if (!string.IsNullOrEmpty(response) && bool.Parse(response))
            {
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }
    //
    //
    // private static async Task<bool> SendEmailWithPost(string url, object data)
    // {
    //     try
    //     {
    //         var response = await (url)
    //             .WithHeader("Accept", "application/json")
    //             .WithHeader("User-Agent", "Kavita")
    //             .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
    //             .WithHeader("x-kavita-version", BuildInfo.Version)
    //             .WithHeader("Content-Type", "application/json")
    //             .WithTimeout(TimeSpan.FromSeconds(30))
    //             .PostJsonAsync(data);
    //
    //         if (response.StatusCode != StatusCodes.Status200OK)
    //         {
    //             return false;
    //         }
    //     }
    //     catch (Exception)
    //     {
    //         return false;
    //     }
    //     return true;
    // }
    //
    private async Task SendEmail(EmailOptionsDto userEmailOptions)
    {
        var smtpConfig = await _unitOfWork.SettingsRepository.GetSmtpConfig();
        var mail = new MailMessage
        {
            Subject = userEmailOptions.Subject,
            Body = userEmailOptions.Body,
            From = new MailAddress(smtpConfig.SenderAddress, smtpConfig.SenderDisplayName),
            IsBodyHtml = smtpConfig.IsBodyHtml
        };

        foreach (var toEmail in userEmailOptions.ToEmails)
        {
            mail.To.Add(toEmail);
        }


        var smtpClient = new SmtpClient
        {
            Host = smtpConfig.Host,
            Port = 587,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtpConfig.SenderAddress, smtpConfig.Password),
            Timeout = 20000
        };

        mail.BodyEncoding = Encoding.Default;

        await smtpClient.SendMailAsync(mail);
    }

    private static string GetEmailBody(string templateName)
    {
        var templateDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "templates", TemplatePath);
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

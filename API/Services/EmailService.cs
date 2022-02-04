using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Email;
using API.Entities.Enums;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IEmailService
{
    Task SendConfirmationEmail(ConfirmationEmailDto data);
    Task<bool> CheckIfAccessible(string host);
    Task SendMigrationEmail(EmailMigrationDto data);
    Task SendPasswordResetEmail(PasswordResetEmailDto data);
    Task<bool> TestConnectivity(string emailUrl);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// This is used to initially set or reset the ServerSettingKey. Do not access from the code, access via UnitOfWork
    /// </summary>
    public const string DefaultApiUrl = "https://email.kavitareader.com";

    public EmailService(ILogger<EmailService> logger, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;

        FlurlHttp.ConfigureClient(DefaultApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    public async Task<bool> TestConnectivity(string emailUrl)
    {
        FlurlHttp.ConfigureClient(emailUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());

        return await SendEmailWithGet(emailUrl + "/api/email/test");
    }

    public async Task SendConfirmationEmail(ConfirmationEmailDto data)
    {
        var success = await SendEmailWithPost(DefaultApiUrl + "/api/email/confirm", data);
        if (!success)
        {
            _logger.LogError("There was a critical error sending Confirmation email");
        }
    }

    public async Task<bool> CheckIfAccessible(string host)
    {
        // This is the only exception for using the default because we need an external service to check if the server is accessible for emails
        return await SendEmailWithGet(DefaultApiUrl + "/api/email/reachable?host=" + host);
    }

    public async Task SendMigrationEmail(EmailMigrationDto data)
    {
        var emailLink = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EmailServiceUrl)).Value;
        await SendEmailWithPost(emailLink + "/api/email/email-migration", data);
    }

    public async Task SendPasswordResetEmail(PasswordResetEmailDto data)
    {
        var emailLink = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EmailServiceUrl)).Value;
        await SendEmailWithPost(emailLink + "/api/email/email-password-reset", data);
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


    private static async Task<bool> SendEmailWithPost(string url, object data)
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
                .PostJsonAsync(data);

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

}

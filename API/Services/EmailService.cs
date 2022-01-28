using System;
using System.Net.Http;
using System.Threading.Tasks;
using API.DTOs.Email;
using API.Services.Tasks;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IEmailService
{
    Task SendConfirmationEmail(ConfirmationEmailDto data);
    Task<bool> CheckIfAccessible(string host);
    Task SendMigrationEmail(EmailMigrationDto data);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private const string ApiUrl = "http://localhost:5003";

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;

        FlurlHttp.ConfigureClient(ApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    public async Task SendConfirmationEmail(ConfirmationEmailDto data)
    {

        var success = await SendEmailWithPost(ApiUrl + "/api/email/confirm", data);
        if (!success)
        {
            _logger.LogError("There was a critical error sending Confirmation email");
        }
    }

    public async Task<bool> CheckIfAccessible(string host)
    {
        return await SendEmailWithGet(ApiUrl + "/api/email/reachable?host=" + host);
    }

    public async Task SendMigrationEmail(EmailMigrationDto data)
    {
        await SendEmailWithPost(ApiUrl + "/api/email/email-migration", data);
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
        catch (Exception e)
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
        catch (Exception e)
        {
            return false;
        }
        return true;
    }
}

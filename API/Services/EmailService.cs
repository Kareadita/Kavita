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
        var responseContent = string.Empty;

        try
        {
            var response = await (ApiUrl + "/api/email/confirm")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(30))
                .PostJsonAsync(data);

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                _logger.LogError("There was a critical error sending Confirmation email. {Content}", response);
            }
        }
        catch (HttpRequestException e)
        {
            var info = new
            {
                dataSent = data,
                response = responseContent
            };

            _logger.LogError(e, "There was a critical error sending Confirmation email. {Content}", info);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was a critical error sending Confirmation email");
        }
    }

    public async Task<bool> CheckIfAccessible(string host)
    {
        try
        {
            var response = await (ApiUrl + "/api/email/reachable?host=" + host)
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
}

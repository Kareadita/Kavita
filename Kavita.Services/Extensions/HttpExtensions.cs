using Kavita.Models.DTOs.Settings;
using Microsoft.AspNetCore.Http;

namespace Kavita.Services.Extensions;

public static class HttpExtensions
{
    public static string GetInstanceUrl(this HttpRequest request, ServerSettingDto serverSettings)
    {
        string origin;

        if (!string.IsNullOrEmpty(serverSettings.HostName))
        {
            origin = serverSettings.HostName.TrimEnd('/');
        }
        else
        {
            var host = request.Host;
            if (host.HasValue && !string.IsNullOrEmpty(host.Host))
            {
                origin = $"{request.Scheme}://{host.Value}";
            }
            else
            {
                var local = request.HttpContext.Connection.LocalPort;
                origin = $"{request.Scheme}://{request.Host.Host}:{local}";
            }
        }

        if (!string.IsNullOrEmpty(serverSettings.BaseUrl))
            origin += serverSettings.BaseUrl.TrimEnd('/');

        return origin + "/";
    }
}

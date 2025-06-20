#nullable enable
using System.Threading.Tasks;
using API.DTOs.Scrobbling;
using API.Extensions;
using Flurl.Http;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;

/// <summary>
/// All Http requests to K+ should be contained in this service, the service will not handle any errors.
/// This is expected from the caller.
/// </summary>
public interface IKavitaPlusApiService
{
    Task<bool> HasTokenExpired(string license, string token, ScrobbleProvider provider);
    Task<int> GetRateLimit(string license, string token);
    Task<ScrobbleResponseDto> PostScrobbleUpdate(ScrobbleDto data, string license);
}

public class KavitaPlusApiService(ILogger<KavitaPlusApiService> logger): IKavitaPlusApiService
{
    private const string ScrobblingPath = "/api/scrobbling/";

    public async Task<bool> HasTokenExpired(string license, string token, ScrobbleProvider provider)
    {
        var res = await Get(ScrobblingPath + "valid-key?provider=" + provider + "&key=" + token, license, token);
        var str = await res.GetStringAsync();
        return bool.Parse(str);
    }

    public async Task<int> GetRateLimit(string license, string token)
    {
        var res = await Get(ScrobblingPath + "rate-limit?accessToken=" + token, license, token);
        var str = await res.GetStringAsync();
        return int.Parse(str);
    }

    public async Task<ScrobbleResponseDto> PostScrobbleUpdate(ScrobbleDto data, string license)
    {
        return await PostAndReceive<ScrobbleResponseDto>(ScrobblingPath + "update", data, license);
    }

    /// <summary>
    /// Send a GET request to K+
    /// </summary>
    /// <param name="url">only path of the uri, the host is added</param>
    /// <param name="license"></param>
    /// <param name="aniListToken"></param>
    /// <returns></returns>
    private static async Task<IFlurlResponse> Get(string url, string license, string? aniListToken = null)
    {
        return await (Configuration.KavitaPlusApiUrl + url)
            .WithKavitaPlusHeaders(license, aniListToken)
            .GetAsync();
    }

    /// <summary>
    /// Send a POST request to K+
    /// </summary>
    /// <param name="url">only path of the uri, the host is added</param>
    /// <param name="body"></param>
    /// <param name="license"></param>
    /// <param name="aniListToken"></param>
    /// <typeparam name="T">Return type</typeparam>
    /// <returns></returns>
    private static async Task<T> PostAndReceive<T>(string url, object body, string license, string? aniListToken = null)
    {
        return await (Configuration.KavitaPlusApiUrl + url)
            .WithKavitaPlusHeaders(license, aniListToken)
            .PostJsonAsync(body)
            .ReceiveJson<T>();
    }
}

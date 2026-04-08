using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Helpers;

namespace Kavita.Services;

public class UrlValidationService(ILocalizationService localizationService) : IUrlValidationService
{
    public async Task ValidateUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new KavitaException(await localizationService.Translate("url-malformed"));
        }

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new KavitaException(await localizationService.Translate("url-https-only"));
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host);
        }
        catch (SocketException)
        {
            throw new KavitaException(await localizationService.Translate("url-unable-to-resolve"));
        }

        if (addresses.Length == 0)
        {
            throw new KavitaException(await localizationService.Translate("url-unable-to-resolve"));
        }

        foreach (var address in addresses)
        {
            if (IpBlocklist.IsBlockedAddress(address))
            {
                throw new KavitaException(await localizationService.Translate("url-blocked-address"));
            }
        }
    }
}

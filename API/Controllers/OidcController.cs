using System.Threading.Tasks;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("[controller]")]
public class OidcController: ControllerBase
{

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string returnUrl = "/")
    {
        if (returnUrl == "/")
        {
            returnUrl = Configuration.BaseUrl;
        }

        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        return Challenge(properties, IdentityServiceExtensions.OpenIdConnect);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {

        if (!Request.Cookies.ContainsKey(OidcService.CookieName))
        {
            return Redirect(Configuration.BaseUrl);
        }

        var res = await Request.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!res.Succeeded || res.Properties == null || string.IsNullOrEmpty(res.Properties.GetTokenValue(OidcService.IdToken)))
        {
            HttpContext.Response.Cookies.Delete(OidcService.CookieName);
            return Redirect(Configuration.BaseUrl);
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = Configuration.BaseUrl+"login" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            IdentityServiceExtensions.OpenIdConnect);
    }

}

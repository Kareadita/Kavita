using API.Extensions;
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
    public IActionResult Login(string returnUrl = "/oidc/callback")
    {
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        return Challenge(properties, IdentityServiceExtensions.OpenIdConnect);
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/login" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            IdentityServiceExtensions.OpenIdConnect);
    }

}

using System;
using Kavita.Server.Middleware;
using Microsoft.AspNetCore.Authorization;

namespace Kavita.Server.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class KoboDeviceAuthenticateAttribute : AuthorizeAttribute
{
    public KoboDeviceAuthenticateAttribute()
    {
        AuthenticationSchemes = KoboDeviceAuthenticationOptions.SchemeName;
    }
}

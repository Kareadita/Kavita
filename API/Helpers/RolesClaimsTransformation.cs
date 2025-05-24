using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Kavita.Common;
using Microsoft.AspNetCore.Authentication;

namespace API.Helpers;

/// <summary>
/// Adds assigned roles from Keycloak under the default <see cref="ClaimTypes.Role"/> claim
/// </summary>
public class RolesClaimsTransformation: IClaimsTransformation
{
    private const string ResourceAccessClaim = "resource_access";
    private string _clientId;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var resourceAccess = principal.FindFirst(ResourceAccessClaim);
        if (resourceAccess == null) return Task.FromResult(principal);

        var resources = JsonSerializer.Deserialize<Dictionary<string, Resource>>(resourceAccess.Value);
        if (resources == null) return Task.FromResult(principal);

        if (string.IsNullOrEmpty(_clientId))
        {
            _clientId = Configuration.OidcClientId;
        }

        var kavitaResource = resources.GetValueOrDefault(_clientId);
        if (kavitaResource == null) return Task.FromResult(principal);

        foreach (var role in kavitaResource.Roles)
        {
            ((ClaimsIdentity)principal.Identity)?.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        return Task.FromResult(principal);
    }

    private sealed class Resource
    {
        [JsonPropertyName("roles")]
        public IList<string> Roles { get; set; } = [];
    }
}

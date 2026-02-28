using Auth.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Controllers;

/// <summary>
/// JWKS endpoint — serves the public keys used to verify JWT signatures.
/// Consumers (API gateways, other services) use this to validate tokens
/// without needing the private key.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class JwksController(IKeyStoreService keyStoreService) : ControllerBase
{
    /// <summary>Returns the JSON Web Key Set (public keys only).</summary>
    [HttpGet("/.well-known/jwks.json")]
    public async Task<IActionResult> GetJwks(CancellationToken ct)
    {
        var keys = await keyStoreService.GetValidationKeysAsync(ct);

        var jwks = new JsonWebKeySet();
        foreach (var (rsaKey, kid) in keys)
        {
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
            jwk.Kid = kid;
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;

            // Strip private key components — only expose public key
            jwk.D = null;
            jwk.P = null;
            jwk.Q = null;
            jwk.DP = null;
            jwk.DQ = null;
            jwk.QI = null;

            jwks.Keys.Add(jwk);
        }

        return Ok(jwks);
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Subsonic;

namespace TlmcPlayerBackend.Auth;

/// <summary>
/// Authenticates native endpoints with the same per-device api_key rows the
/// Subsonic facade uses — the fork's durable credential is its API key, not a
/// live Keycloak session, so endpoints it must reach continuously (play
/// reporting, and later the recommendation surfaces) opt in via
/// [Authorize(AuthenticationSchemes = ...)]. Everything else stays
/// Keycloak-only. Key arrives as X-Api-Key, or api_key in the query for
/// senders that cannot set headers (sendBeacon).
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TlmcApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = Request.Headers["X-Api-Key"].ToString();
        if (key.Length == 0)
        {
            key = Request.Query["api_key"].ToString();
        }

        if (key.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var hash = SubsonicApiKey.Hash(key);
        var row = await db.ApiKeys.AsNoTracking()
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash);
        if (row == null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        // ToUserClaim requires both claims; preferred_username carries the
        // profile's display name, same as a Keycloak token would.
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, row.UserId.Value.ToString()),
                new Claim("preferred_username", row.User.DisplayName),
            ],
            SchemeName);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Subsonic;

/// <summary>
/// Marks a /rest action reachable without credentials even when
/// <see cref="SubsonicOptions.AllowAnonymous"/> is off. The spec requires it
/// for getOpenSubsonicExtensions: clients probe capabilities before they know
/// which auth mechanism to use.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SubsonicNoAuthAttribute : Attribute;

/// <summary>
/// The /rest gate (Docs/SUBSONIC.md section 4): the facade answers 404 wholesale
/// when disabled, and authenticates via the OpenSubsonic apiKeyAuthentication
/// extension only. Legacy `u`/`t`/`s`/`p` credentials are refused with the
/// protocol's own error codes — supporting them would require storing
/// recoverable passwords next to Keycloak.
/// </summary>
public class SubsonicAuthFilter(AppDbContext context, IOptions<SubsonicOptions> options) : IAsyncResourceFilter
{
    /// <summary>HttpContext.Items key holding the authenticated UserId (or null).</summary>
    public const string UserItem = "SubsonicUser";

    private static readonly TimeSpan LastUsedGranularity = TimeSpan.FromHours(1);

    private readonly AppDbContext _context = context;
    private readonly SubsonicOptions _options = options.Value;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!_options.Enabled)
        {
            // Indistinguishable from a route that does not exist, matching the
            // internal API's fail-closed posture.
            context.Result = new NotFoundResult();
            return;
        }

        var request = context.HttpContext.Request;
        var query = request.Query;

        string Param(string name)
        {
            var fromQuery = query[name].ToString();
            if (fromQuery.Length > 0 || !request.HasFormContentType)
            {
                return fromQuery;
            }

            return request.Form[name].ToString();
        }

        var apiKey = Param("apiKey");
        var hasLegacy = Param("u").Length > 0 || Param("p").Length > 0
            || Param("t").Length > 0 || Param("s").Length > 0;

        if (hasLegacy && apiKey.Length > 0)
        {
            context.Result = SubsonicResult.Error(SubsonicErrorCodes.ConflictingAuthMechanisms,
                "Provide either apiKey or username credentials, not both");
            return;
        }

        if (hasLegacy)
        {
            // Practically every client (and all hosted web clients) can only log
            // in with u/t/s. On an anonymous instance those credentials are
            // accepted — any username, any password — and grant exactly the
            // anonymous surface: there is no identity behind them, so user-scoped
            // endpoints still answer 50. A keyed instance refuses them outright.
            if (_options.AllowAnonymous)
            {
                context.HttpContext.Items[UserItem] = null;
                await next();
                return;
            }

            context.Result = SubsonicResult.Error(SubsonicErrorCodes.AuthMechanismNotSupported,
                "This server only supports API key authentication (apiKey parameter)");
            return;
        }

        var skipAuth = (context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?
            .MethodInfo.IsDefined(typeof(SubsonicNoAuthAttribute), inherit: true) == true;

        UserId? user = null;
        if (apiKey.Length > 0)
        {
            var hash = SubsonicApiKey.Hash(apiKey);
            var row = await _context.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash);
            if (row == null)
            {
                context.Result = SubsonicResult.Error(SubsonicErrorCodes.InvalidApiKey, "Invalid API key");
                return;
            }

            user = row.UserId;

            // last_used_at is display metadata; one write per key per hour, not
            // one per request.
            var now = DateTime.UtcNow;
            if (row.LastUsedAt == null || now - row.LastUsedAt > LastUsedGranularity)
            {
                row.LastUsedAt = now;
                await _context.SaveChangesAsync();
            }
        }
        else if (!_options.AllowAnonymous && !skipAuth)
        {
            context.Result = SubsonicResult.Error(SubsonicErrorCodes.NotAuthenticated,
                "Authentication required (apiKey parameter)");
            return;
        }

        context.HttpContext.Items[UserItem] = user;
        await next();
    }
}

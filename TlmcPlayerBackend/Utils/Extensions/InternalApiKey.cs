using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TlmcPlayerBackend.Utils.Extensions;

/// <summary>
/// Gate for the internal write API: requires a shared secret in the
/// <c>X-Internal-Api-Key</c> header matching <c>Internal:ApiKey</c> from
/// configuration.
///
/// This replaces gating on <c>ASPNETCORE_ENVIRONMENT</c>, which was wrong in both
/// directions. It let the entire unauthenticated write surface -- create albums and
/// tracks, JSON-Patch entities, register assets by filesystem path -- be reachable
/// by anyone on any deployment configured as Development, which the repository's own
/// compose file did. And because real deployments run as Production, those endpoints
/// answered 404 there, so the ETL could only publish to an instance deliberately
/// configured the unsafe way.
///
/// Fails closed: with no key configured the endpoints stay invisible (404), which is
/// what a default deployment saw before.
/// </summary>
public class InternalApiKey : Attribute, IResourceFilter
{
    public const string HeaderName = "X-Internal-Api-Key";
    public const string ConfigKey = "Internal:ApiKey";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var configured = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()[ConfigKey];

        if (string.IsNullOrEmpty(configured))
        {
            // Indistinguishable from a route that does not exist, so an unconfigured
            // deployment does not advertise that an internal API is there at all.
            context.Result = new NotFoundResult();
            return;
        }

        var presented = context.HttpContext.Request.Headers[HeaderName].ToString();

        // Fixed-time comparison: a plain string equality check leaks how much of the
        // key is correct through response timing.
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configured),
            Encoding.UTF8.GetBytes(presented));

        if (!matches)
        {
            context.Result = new UnauthorizedResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}

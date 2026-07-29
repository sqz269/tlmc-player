using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakAuthProvider.Authentication;

public static class ConfigureAuthServiceExt
{
    public static void ConfigureJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var keycloak = configuration.GetSection("Keycloak");

        var realmUrl = keycloak["RealmUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(realmUrl))
        {
            throw new InvalidOperationException("Keycloak:RealmUrl is not configured");
        }

        // Mirrors the framework's own option name. On by default; only a local
        // cluster reaching Keycloak over plain HTTP should turn it off.
        var requireHttps = keycloak.GetValue<bool?>("RequireHttpsMetadata") ?? true;

        var authBuilder = services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authBuilder.AddJwtBearer(o =>
        {
            // Discovery is left to JwtBearer's own ConfigurationManager, which fetches
            // the document lazily on first use and refreshes it periodically. This
            // used to resolve the signing keys once, synchronously, during service
            // registration, which had two consequences: the API could not start at
            // all while Keycloak was unreachable -- now a hard dependency on the
            // Cloudflare tunnel being up -- and after a realm key rotation every
            // token failed until someone restarted the process.
            o.Authority = realmUrl;
            o.RequireHttpsMetadata = requireHttps;

            // Re-fetch the document when a token is signed by an unrecognised key,
            // so a rotation costs one failed validation rather than an outage.
            o.RefreshOnIssuerKeyNotFound = true;

            #region == JWT Token Validation ===
            o.TokenValidationParameters = new TokenValidationParameters
            {
                // Keycloak's default audience for a browser client is "account"
                // rather than this API, so audience validation stays off; the issuer
                // check below is what constrains which realm may mint accepted
                // tokens. Enabling it needs a dedicated audience mapper in the realm.
                ValidateAudience = false,
                ValidateIssuer = true,
                ValidIssuers = new[] { realmUrl },
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true
            };
            #endregion
            #region === Event Authentification Handlers ===
            o.Events = new JwtBearerEvents()
            {
                // Only logs. Exception detail is never written to the response: in
                // development this previously returned Exception.ToString(), handing an
                // unauthenticated caller a stack trace naming internal types and the
                // configured issuer.
                //
                // It also must not write the response at all. The challenge below runs
                // afterwards, and completing the response here made its own attempt to
                // set a status throw "StatusCode cannot be set because the response has
                // already started" on every rejected token.
                OnAuthenticationFailed = c =>
                {
                    if (c.Exception is not SecurityTokenExpiredException)
                    {
                        c.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("KeycloakAuthProvider.Authentication")
                            .LogWarning(c.Exception, "JWT bearer authentication failed");
                    }

                    return Task.CompletedTask;
                },

                // A rejected token is the caller's problem, not a server fault, so this
                // answers 401. Returning 500 -- as this used to for everything except
                // expiry -- also defeats client refresh logic, which retries on 401 and
                // gives up on 5xx.
                OnChallenge = async c =>
                {
                    // Suppresses the default challenge so it does not also try to write.
                    c.HandleResponse();

                    c.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    c.Response.ContentType = "text/plain";

                    // Awaited rather than .Wait(): this runs on a request thread, and
                    // blocking here starves the thread pool under load.
                    await c.Response.WriteAsync(c.AuthenticateFailure switch
                    {
                        SecurityTokenExpiredException => "The token is expired",
                        null => "Authentication required",
                        _ => "The token could not be validated"
                    });
                }
            };
            #endregion
        });
    }
}

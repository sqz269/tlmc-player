using KeycloakAuthProvider.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace KeycloakAuthProvider.Authentication;

public static class ConfigureAuthServiceExt
{
    public static void ConfigureJwt(this IServiceCollection services)
    {
        // get oidc config provider service
        var oidcConfigProvider = services.BuildServiceProvider().GetRequiredService<OpenIdConnectConfigurationProviderService>();
        var isDevelopment = services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        var authBuilder = services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        var signingKeys = oidcConfigProvider.GetSigningKeysAsync().GetAwaiter().GetResult();

        authBuilder.AddJwtBearer(o =>
        {

            #region == JWT Token Validation ===
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = true,
                ValidIssuers = new[] { oidcConfigProvider.RealmUrl },
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                TryAllIssuerSigningKeys = true,
                ValidateLifetime = true
            };
            #endregion
            #region === Event Authentification Handlers ===
            o.Events = new JwtBearerEvents()
            {
                OnTokenValidated = c =>
                {
                    Console.WriteLine("User successfully authenticated");
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = c =>
                {
                    if (c.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        c.NoResult();
                        c.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        c.Response.ContentType = "text/plain";
                        c.Response.WriteAsync("The token is expired").Wait();
                        c.Response.CompleteAsync().Wait();

                        return Task.CompletedTask;
                    }

                    if (isDevelopment == false)
                    {
                        c.NoResult();
                        c.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        c.Response.ContentType = "text/plain";
                        c.Response.WriteAsync("An error occurred processing your authentication.").Wait();
                        Console.Write("AUTHENTICATION FAILED");
                        Console.WriteLine(c.Exception.ToString());
                        c.Response.CompleteAsync().Wait();

                        return Task.CompletedTask;
                    }

                    c.NoResult();
                    c.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    c.Response.ContentType = "text/plain";
                    c.Response.WriteAsync(c.Exception.ToString()).Wait();
                    c.Response.CompleteAsync().Wait();

                    return Task.CompletedTask;
                }
            };
            #endregion
        });
    }
}
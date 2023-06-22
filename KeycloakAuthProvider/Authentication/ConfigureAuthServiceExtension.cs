using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakAuthProvider.Authentication;

public static class ConfigureAuthServiceExt
{
    private static RsaSecurityKey BuildRsaPublicKey(string publicKey)
    {
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        var key = new RsaSecurityKey(rsa);
        return key;
    }

    public static void ConfigureJwt(this IServiceCollection services, bool isDevelopment, string publicKeyJwt, string realmUrl)
    {
        var authBuilder = services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authBuilder.AddJwtBearer(o =>
        {

            #region == JWT Token Validation ===
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = true,
                ValidIssuers = new[] { realmUrl },
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = BuildRsaPublicKey(publicKeyJwt),
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
                    else if (isDevelopment == false)
                    {
                        c.NoResult();
                        c.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        c.Response.ContentType = "text/plain";
                        c.Response.WriteAsync("An error occurred processing your authentication.").Wait();
                        c.Response.CompleteAsync().Wait();

                        return Task.CompletedTask;
                    }
                    else
                    {
                        c.NoResult();
                        c.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        c.Response.ContentType = "text/plain";
                        c.Response.WriteAsync(c.Exception.ToString()).Wait();
                        c.Response.CompleteAsync().Wait();

                        return Task.CompletedTask;
                    }
                }
            };
            #endregion
        });
    }
}
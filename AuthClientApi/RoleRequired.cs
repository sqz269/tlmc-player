using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthServiceClientApi;

public class UserClaim
{
    public Guid? UserId { get; set; }
    public string? Username { get; set; }

    public UserClaim(Guid? userId, string? username)
    {
        UserId = userId;
        Username = username;
    }
}

public class RoleRequired : ActionFilterAttribute
{
    private readonly HashSet<string>? _rolesRequired;

    public RoleRequired(params string[]? roles)
    {
        if (roles != null) _rolesRequired = new HashSet<string>(roles);
    }

    private static ObjectResult Unauthenticated()
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Not Authenticated"
        })
        {
            StatusCode = StatusCodes.Status401Unauthorized,
        };
    }

    private static ObjectResult NoAccess()
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Not Authenticated"
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var jwtManager = context.HttpContext.RequestServices.GetService<JwtManager>();

        if (jwtManager == null)
        {
            throw new InvalidOperationException("Unable to Validate Authorization. JwtManager not configured");
        }

        if (_rolesRequired == null || _rolesRequired.Count == 0 || _rolesRequired.Contains(KnownRoles.Guest))
        {
            context.HttpContext.Items.Add("CustUser", new UserClaim(null, null));
            return;
        }

        // Get Authorization header
        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        // we only look for string begin with Jwt
        var jwt = authorization.Split("Jwt ")[^1];

        if (string.IsNullOrWhiteSpace(jwt))
        {
            context.Result = Unauthenticated();
            return;
        }

        AuthToken? token;
        try
        {
            token = jwtManager.DecodeJwt<AuthToken>(jwt);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException)
        {
            Console.WriteLine($"--> Error while decoding Jwt: {e.Message}");
            // Cannot decode JWT because either invalid jwt is provided or the signature is invalid
            context.Result = Unauthenticated();
            return;
        }
        // Probably just plain broken jwt
        catch (Exception e)
        {
            context.Result = Unauthenticated();
            return;
        }

        if (token == null)
        {
            context.Result = Unauthenticated();
            return;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > token.Expiration)
        {
            context.Result = Unauthenticated();
            return;
        }
        
        // User does not sufficient role
        if (!token.Roles.Any(role => _rolesRequired.Contains(role)))
        {
            context.Result = NoAccess();
            return;
        }

        context.HttpContext.Items.Add("UserClaim", new UserClaim(Guid.Parse(token.UserId), token.User));
        return;
    }
}
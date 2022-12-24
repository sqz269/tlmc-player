using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthServiceClientApi;

public class RoleRequired : ActionFilterAttribute
{
    private readonly HashSet<string>? _rolesRequired;

    public RoleRequired(params string[]? roles)
    {
        if (roles != null) _rolesRequired = new HashSet<string>(roles);
    }

    private static ObjectResult Unauthorized()
    {
        return new ObjectResult(new { Success = false, Message = "Authorization Required" })
            { StatusCode = StatusCodes.Status401Unauthorized };
    }

    private static ObjectResult NoAccess()
    {
        return new ObjectResult(new { Success = false, Message = "Insufficient Access" })
            { StatusCode = StatusCodes.Status403Forbidden };
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var jwtManager = context.HttpContext.RequestServices.GetService<JwtManager>();

        if (jwtManager == null)
        {
            throw new InvalidOperationException("Unable to Validate Authorization. JwtManager not configured");
        }

        if (_rolesRequired == null || _rolesRequired.Count == 0 || _rolesRequired.Contains(KnownRoles.Guest))
            return;

        // Get Authorization header
        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        // we only look for string begin with Jwt
        var jwt = authorization.Split("Jwt ")[^1];

        if (string.IsNullOrWhiteSpace(jwt))
        {
            context.Result = Unauthorized();
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
            context.Result = Unauthorized();
            return;
        }
        // Probably just plain broken jwt
        catch (Exception e)
        {
            context.Result = Unauthorized();
            return;
        }

        if (token == null)
        {
            context.Result = Unauthorized();
            return;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > token.Expiration)
        {
            context.Result = Unauthorized();
            return;
        }
        
        // Authorization succeeds
        if (token.Roles.Any(role => _rolesRequired.Contains(role)))
        {
            return;
        }

        context.Result = NoAccess();
    }
}
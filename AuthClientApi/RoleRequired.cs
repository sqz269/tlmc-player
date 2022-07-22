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

    private UnauthorizedObjectResult GenerateUnauthorizedObjectResult()
    {
        return new UnauthorizedObjectResult(new { Success = false, Message = "Authorization Required" });
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var jwtManager = context.HttpContext.RequestServices.GetService<JwtManager>();

        if (jwtManager == null)
        {
            throw new InvalidOperationException("Unable to Authorize request. JwtManager not configured");
        }

        if (_rolesRequired == null || _rolesRequired.Count == 0)
            return;

        // Get Authorization header
        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        // we only look for string begin with Jwt
        var jwt = authorization.Split("Jwt ")[^1];

        if (string.IsNullOrWhiteSpace(jwt))
        {
            context.Result = GenerateUnauthorizedObjectResult();
            return;
        }

        AuthToken? token = null;
        try
        {
            token = jwtManager.DecodeJwt<AuthToken>(jwt);
        }
        catch (Exception e) when(e is InvalidOperationException or InvalidDataException)
        {
            Console.WriteLine($"--> Error while decoding Jwt: {e.Message}");
            // Cannot decode JWT because either invalid jwt is provided or the signature is invalid
            context.Result = GenerateUnauthorizedObjectResult();
            return;
        }

        if (token == null)
        {
            context.Result = GenerateUnauthorizedObjectResult();
            return;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > token.Expiration)
        {
            context.Result = GenerateUnauthorizedObjectResult();
            return;
        }
        
        // Authorization succeeds
        if (token.Roles.Any(role => _rolesRequired.Contains(role)))
        {
            return;
        }

        context.Result = new JsonResult(new { Success = false, Message = "You do not have sufficient privileges to access this resource" })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
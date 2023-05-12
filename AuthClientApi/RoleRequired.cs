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

    private static ObjectResult Unauthenticated(string details)
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Not Authenticated",
            Detail = details
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
            Title = "Insufficient Permission"
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }

    private Tuple<bool, UserClaim?, ObjectResult?> InvalidAuth(ObjectResult result)
    {
        return new Tuple<bool, UserClaim?, ObjectResult?>(false, null, result);
    }

    private Tuple<bool, UserClaim?, ObjectResult?> ValidAuth(UserClaim claim)
    {
        return new Tuple<bool, UserClaim?, ObjectResult?>(true, claim, null);
    }

    private async Task<Tuple<bool, UserClaim?, ObjectResult?>> ValidateUserAuth(JwtManager jwtManager,
        ActionExecutingContext context)
    {
        // ValidUserAuth will still still go though with token verification even if the 
        // accessed resources does not require any privilege at all. This is done because
        // some resources can perform more actions if the user is authed.
        // For example: you can still call the GetUserPlaylists API if you are not authenticated
        // but it will only return the user's public playlist. However, if you call it will the
        // valid user's token, you will retrieve all the user's playlist
        var results = await _ValidateUserAuth(jwtManager, context);

        // If the resources does not require Authentication, but
        // the authentication failed, we still want to proceed with the request
        if (_rolesRequired.Contains(KnownRoles.Guest) &&
            !results.Item1)
        {
            return ValidAuth(new UserClaim(null, null));
        }

        return results;
    }

    private async Task<Tuple<bool, UserClaim?, ObjectResult?>> _ValidateUserAuth(JwtManager jwtManager, 
        ActionExecutingContext context)
    {
        // Get Authorization header
        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();

        if ((_rolesRequired == null ||
             _rolesRequired.Count == 0 ||
             _rolesRequired.Contains(KnownRoles.Guest)) && 
            authorization.Length == 0)
        {
            return ValidAuth(new UserClaim(null, null));
        }

        // we only look for string begin with Jwt
        var jwt = authorization.Split("Jwt ")[^1];

        if (string.IsNullOrWhiteSpace(jwt))
        {
            return InvalidAuth(Unauthenticated("Invalid Jwt Token. Token null or empty"));
        }

        AuthToken? token;
        try
        {
            token = await jwtManager.DecodeJwt<AuthToken>(jwt);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException)
        {
            Console.WriteLine($"--> Error while decoding Jwt: {e.Message}");
            // Cannot decode JWT because either invalid jwt is provided or the signature is invalid
            return InvalidAuth(Unauthenticated("Invalid Jwt Token. Token Decode Error (Signature Error)"));
        }
        // Probably just plain broken jwt
        catch (Exception e)
        {
            return InvalidAuth(Unauthenticated("Invalid Jwt Token. Bad Token"));
        }

        if (token == null)
        {
            return InvalidAuth(Unauthenticated("Invalid Jwt Token"));
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > token.Expiration)
        {
            return InvalidAuth(Unauthenticated("Expired Jwt Token"));
        }

        // User does not sufficient role
        // TODO: USER COULD HAVE NO ROLES AT ALL. BUT WE STILL NEED TO ALLOW
        // THEM TO ACCESS THE RESOURCE, (SOMETIMES)
        if (!token.Roles.Any(role => _rolesRequired.Contains(role)))
        {
            return InvalidAuth(NoAccess());
        }

        return ValidAuth(new UserClaim(Guid.Parse(token.UserId), token.User));
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var jwtManager = context.HttpContext.RequestServices.GetService<JwtManager>();

        if (jwtManager == null)
        {
            throw new InvalidOperationException("Unable to Validate Authorization. JwtManager not configured");
        }

        var (isValid, claim, errorObject) = await ValidateUserAuth(jwtManager, context);

        if (isValid)
        {
            context.HttpContext.Items.Add("UserClaim", claim);

            await next();
        }
        else
        {
            context.Result = errorObject;
        }
    }
}
using System.Text.RegularExpressions;
using AuthenticationService.Data;
using AuthenticationService.Dtos;
using AuthenticationService.Extensions;
using AuthenticationService.Models.Api;
using AuthenticationService.Models.Db;
using AuthServiceClientApi;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationService.Controllers;

[Route("api/user")]
[ApiController]
public class UserController : Controller
{
    private readonly IUserRepo _userRepo;
    private readonly IRoleRepo _roleRepo;
    private readonly IMapper _mapper;
    private readonly JwtManager _jwtManager;

    // TODO: Make this a configuration field
    public static TimeSpan JwtExpOffset { get; } = TimeSpan.FromHours(3);

    public UserController(
        IUserRepo userRepo,
        IRoleRepo roleRepo,
        IMapper mapper,
        JwtManager jwtManager)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _mapper = mapper;
        _jwtManager = jwtManager;
    }

    [HttpGet("all", Name = nameof(GetAllUsers))]
    [RoleRequired(KnownRoles.Admin)]
    public IEnumerable<User> GetAllUsers()
    {
        return _userRepo.GetAllUsers();
    }

    /// <summary>
    /// Check if the user credentials are valid based on username and password rules
    ///
    /// <br></br>
    /// Username: 4 - 16 Alphanumeric characters, including dots (.), underscore (_) and dash (-)
    /// Password: 6 - 64 characters (unicode accepted)
    /// </summary>
    /// <param name="userCredentials">The user credential to be checked</param>
    /// <returns>Null if the user credentials are valid, else a string indicating the error</returns>
    private string? PreValidateUserCredentials(UserCredentialsDto userCredentials)
    {
        var passwordValidator = new Regex("^.{6,64}$");
        var usernameValidator = new Regex("^[A-Za-z\\d\\-_.]{4,16}$");

        if (userCredentials.Password.Length is > 64 or < 6)
        {
            return "Password length is not between 6 - 64";
        }

        if (userCredentials.UserName.Length is > 16 or < 4)
        {
            return "Username length is not between 4 - 16";
        }

        // Probably don't need this
        if (!passwordValidator.IsMatch(userCredentials.Password))
        {
            return "Password Length is not between 6 - 64";
        }

        if (!usernameValidator.IsMatch(userCredentials.UserName))
        {
            return "Username contains non-Alphanumeric characters";
        }

        return null;
    }

    [HttpPost]
    [Route("register", Name = nameof(Register))]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<RegisterResult> Register([FromBody] UserCredentialsDto userCredentials)
    {
        string? validationError = PreValidateUserCredentials(userCredentials);

        if (validationError != null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Registration Failed",
                detail: validationError);
        }

        if (_userRepo.DoesUserExist(userCredentials.UserName))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict, 
                title: "Registration Failed", 
                detail: "User with the same username already exists");
        }

        userCredentials.Password = BCrypt.Net.BCrypt.HashPassword(userCredentials.Password);
        var user = _mapper.Map<User>(userCredentials);

        var defaultRole = new Role { RoleName = KnownRoles.User };

        if (!_roleRepo.DoesRoleExist(defaultRole))
        {
            _roleRepo.AddRole(defaultRole);
            _roleRepo.SaveChanges();
        }

        _userRepo.CreateUser(user);
        _userRepo.SaveChanges();

        var dbUser = _userRepo.GetUserFromUsername(user.UserName);

        if (dbUser == null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError, 
                title: "Registration Failed",
                detail: "Internal Server Error: User Insert Failed");
        }

        dbUser.Roles.Add(defaultRole);
        _userRepo.SaveChanges();

        return Ok(new RegisterResult()
        {
            UserId = dbUser.UserId.ToString(),
            Username = dbUser.UserName
        });
    }

    private async Task<LoginResult> LoginUser(User user)
    {
        var token = await user.GetJwtToken(_jwtManager, JwtExpOffset);
        Guid? refreshToken = _userRepo.CreateToken(user).TokenId;

        _userRepo.SaveChanges();

        return new LoginResult
        {
            JwtToken = token.Item1,
            RefreshToken = refreshToken?.ToString(),
            AuthInfo = token.Item2
        };
    }

    [HttpPost]
    [Route("login", Name = nameof(Login))]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResult>> Login([FromBody] UserCredentialsDto userCredentials)
    {
        string? validationError = PreValidateUserCredentials(userCredentials);

        if (validationError != null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Login Failed",
                detail: "Invalid Username or Password");
        }

        var user = _userRepo.GetUserFromUsername(userCredentials.UserName);
        if (user == null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: "Login Failed",
                detail: "Invalid Username or password");
        }


        var isPasswordValid = BCrypt.Net.BCrypt.Verify(userCredentials.Password, user.Password);
        if (!isPasswordValid)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Login Failed",
                detail: "Invalid Username or password");
        }

        return await LoginUser(user);
    }

    [HttpPost]
    [Route("logout")]
    [ProducesResponseType(typeof(OkResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult Logout([FromBody] Guid refreshToken)
    {
        var success = _userRepo.RevokeRefreshToken(refreshToken);

        if (success)
        {
            return Ok();
        }

        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Logout Failed",
            detail: "Failed to revoke refresh token. Maybe the token was already invalid?");
    }
}
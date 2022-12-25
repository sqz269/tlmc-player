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

    [HttpGet("all")]
    [RoleRequired(KnownRoles.Admin)]
    public IEnumerable<User> GetAllUsers()
    {
        return _userRepo.GetAllUsers();
    }

    [HttpPost]
    [Route("register")]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult Register([FromBody] UserCredentialsDto userCredentials)
    {
        if (_userRepo.DoesUserExist(userCredentials.UserName))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "User with the same username already exists");
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
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Transaction failed");
        }

        dbUser.Roles.Add(defaultRole);
        _userRepo.SaveChanges();

        return Ok();
    }

    private LoginResult LoginUser(User user)
    {
        var jwtToken = user.GetJwtToken(_jwtManager, JwtExpOffset);
        Guid? refreshToken = _userRepo.CreateToken(user).TokenId;

        _userRepo.SaveChanges();

        return new LoginResult
        {
            JwtToken = jwtToken,
            RefreshToken = refreshToken?.ToString(),
            Roles = user.Roles.ToList().ConvertAll(role => role.RoleName)
        };
    }

    [HttpPost]
    [Route("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResult> Login([FromBody] UserCredentialsDto userCredentials)
    {
        var user = _userRepo.GetUserFromUsername(userCredentials.UserName);
        if (user == null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid Username or Password");
        }


        var isPasswordValid = BCrypt.Net.BCrypt.Verify(userCredentials.Password, user.Password);
        if (!isPasswordValid)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid Username or Password");
        }
        return LoginUser(user);
    }
}
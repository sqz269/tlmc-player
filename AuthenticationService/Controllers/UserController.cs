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
    [ProducesResponseType(typeof(ApiResponse<>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<>), StatusCodes.Status500InternalServerError)]
    public ActionResult<LoginResult> Register([FromBody] UserCredentialsDto userCredentials)
    {
        if (_userRepo.DoesUserExist(userCredentials.UserName))
        {
            return Conflict(ApiResponse<object>.Fail("User with same username already exists"));
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
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Unable to verify transaction"));

        dbUser.Roles.Add(defaultRole);
        _userRepo.SaveChanges();

        return Ok(ApiResponse<object>.Ok(null));
    }

    private LoginResult? LoginUser(User user)
    {
        if (user.UserId == null)
            throw new ArgumentNullException(nameof(user));

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
    [ProducesResponseType(typeof(ApiResponse<LoginResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResult>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<LoginResult>> Login([FromBody] UserCredentialsDto userCredentials)
    {
        var user = _userRepo.GetUserFromUsername(userCredentials.UserName);
        if (user == null)
            return ApiResponse<LoginResult>.Fail("Invalid Username or Password");


        var isPasswordValid = BCrypt.Net.BCrypt.Verify(userCredentials.Password, user.Password);
        if (!isPasswordValid)
            return ApiResponse<LoginResult>.Fail("Invalid Username or Password");

        return ApiResponse<LoginResult>.Ok(LoginUser(user));
    }
}
using System.Net;
using System.Security.Cryptography;
using AuthenticationService.Data;
using AuthenticationService.Dtos;
using AuthenticationService.Models.Api;
using AuthenticationService.Models.Db;
using AuthenticationService.Extensions;
using AuthServiceClientApi;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : Controller
{
    private readonly IUserRepo _userRepo;
    private readonly IRoleRepo _roleRepo;
    private readonly IMapper _mapper;
    private readonly JwtManager _jwtManager;

    public static TimeSpan JwtExpOffset { get; } = TimeSpan.FromHours(3);

    public AuthController(
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

    private string GenerateJwtTokenForUser(User user, TimeSpan expirationOffset)
    {
        var authToken = user.ToAuthToken(expirationOffset);
        return _jwtManager.GenerateJwt(authToken);
    }

    [HttpGet]
    [Route("jwt/key", Name = nameof(GetJwtPublicKey))]
    [ProducesResponseType(typeof(JwtKeyResponse), StatusCodes.Status200OK)]
    public ActionResult<JwtKeyResponse> GetJwtPublicKey()
    {
        return new JwtKeyResponse()
        {
            PublicKey = _jwtManager.PublicKey
        };
    }

    [HttpPost]
    [Route("token", Name = nameof(GetNewToken))]
    [ProducesResponseType(typeof(JwtRenewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [RoleRequired(KnownRoles.User)]
    public ActionResult<JwtRenewResult> GetNewToken([FromBody] Guid tokenId)
    {
        var user = _userRepo.GetUserFromToken(tokenId);
        if (user == null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid Refresh Token");
        }

        var token = user.GetJwtToken(_jwtManager, JwtExpOffset);
        return Ok(new JwtRenewResult { Token = token.Item1, AuthInfo = token.Item2 });
    }
}
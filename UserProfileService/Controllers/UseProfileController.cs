using System.Security.Cryptography.X509Certificates;
using AutoMapper;
using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using UserProfileService.Data.Api;
using UserProfileService.Dtos;
using UserProfileService.Models;

namespace UserProfileService.Controllers;

[Authorize]
[ApiController]
[Route("api/user")]
public class UseProfileController : Controller
{
    private readonly IMapper _mapper;
    private readonly IUserProfileRepo _userProfileRepo;

    public UseProfileController(IMapper mapper, IUserProfileRepo userProfileRepo)
    {
        _mapper = mapper;
        _userProfileRepo = userProfileRepo;
    }

    [HttpGet("{userId:Guid}")]
    [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileReadDto>> GetUserProfile(Guid userId)
    {
        // TODO: Have profile visibility
        var user = await _userProfileRepo.GetUserProfileById(userId);
        if (user == null)
        {
            return NotFound($"No user with id: {userId} exists");
        }

        return _mapper.Map<UserProfileReadDto>(user);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileReadDto>> GetUserProfile()
    {
        var claim = HttpContext.User.ToUserClaim();

        var user = await _userProfileRepo.GetUserProfileById(claim.UserId.Value);
        if (user == null)
        {
            return NotFound($"No user with id: {claim.UserId} exists");
        }

        return _mapper.Map<UserProfileReadDto>(user);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserProfileReadDto?>> CreateUser([FromBody] UserProfileWriteDto userProfile)
    {
        var claim = HttpContext.User.ToUserClaim();

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existingProfile = await _userProfileRepo.GetUserProfileById(claim.UserId.Value);
        if (existingProfile != null)
        {
            return Conflict(new { Message = "User profile already exists." });
        }

        // create user
        var profile = _mapper.Map<UserProfile>(userProfile);
        profile.Id = claim.UserId.Value;
        profile.DateJoined = DateTime.UtcNow;

        var success = await _userProfileRepo.CreateUserProfile(profile);
        if (success)
        {
            return Ok(_mapper.Map<UserProfileReadDto>(profile));
        }

        return Problem("An error occurred when creating a new user profile", title: "Internal Server Error",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    [Authorize]
    [HttpPatch]
    [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileReadDto?>> UpdateUser([FromBody] JsonPatchDocument<UserProfileUpdateDto> patch)
    {
        var claim = HttpContext.User.ToUserClaim();

        var patchActual = _mapper.Map<JsonPatchDocument<UserProfile>>(patch);

        var targetUser = await _userProfileRepo.GetUserProfileById(claim.UserId.Value);

        if (targetUser == null)
        {
            return BadRequest("User profile not found. Did you create a profile first?");
        }

        patchActual.ApplyTo(targetUser);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = await _userProfileRepo.UpdateUserProfile(targetUser);

        if (success)
        {
            return Ok(_mapper.Map<UserProfileReadDto>(targetUser));
        }

        return Problem("An error occurred when updating the user profile", title: "Internal Server Error",
                       statusCode: StatusCodes.Status500InternalServerError);
    }
}
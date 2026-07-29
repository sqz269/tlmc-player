using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;
using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using TlmcPlayerBackend.Data.Api.UserProfile;
using TlmcPlayerBackend.Dtos.UserProfile;

namespace TlmcPlayerBackend.Controllers.UserProfile;

using TlmcPlayerBackend.Models.UserProfile;

[Authorize]
[ApiController]
[Route("api/user")]
public class UserProfileController : Controller
{
    private readonly IMapper _mapper;
    private readonly IUserProfileRepo _userProfileRepo;

    public UserProfileController(IMapper mapper, IUserProfileRepo userProfileRepo)
    {
        _mapper = mapper;
        _userProfileRepo = userProfileRepo;
    }

    [HttpGet("{userId:Guid}", Name = nameof(GetUserProfile))]
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

    [HttpGet("me", Name = nameof(GetCurrentUserProfile))]
    [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileReadDto>> GetCurrentUserProfile()
    {
        var claim = HttpContext.User.ToUserClaim();

        var user = await _userProfileRepo.GetUserProfileById(claim.UserId);
        
        
        if (user != null) return _mapper.Map<UserProfileReadDto>(user);

        await CreateUser(new UserProfileWriteDto
        {
            DisplayName = claim.Username
        });

        return _mapper.Map<UserProfileReadDto>(await _userProfileRepo.GetUserProfileById(claim.UserId));
    }

    [Authorize]
    [HttpPost("", Name = nameof(CreateUser))]
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

        var existingProfile = await _userProfileRepo.GetUserProfileById(claim.UserId);
        if (existingProfile != null)
        {
            return Conflict(new { Message = "User profile already exists." });
        }

        // create user
        var profile = _mapper.Map<UserProfile>(userProfile);
        profile.Id = claim.UserId;
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
    [HttpPatch("", Name = nameof(UpdateUser))]
    [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileReadDto?>> UpdateUser([FromBody] JsonPatchDocument<UserProfileUpdateDto> patch)
    {
        var claim = HttpContext.User.ToUserClaim();

        var targetUser = await _userProfileRepo.GetUserProfileById(claim.UserId);

        if (targetUser == null)
        {
            return BadRequest("User profile not found. Did you create a profile first?");
        }

        // The patch is applied to a DTO projection and only then mapped onto the
        // entity. Retyping the document to JsonPatchDocument<UserProfile> (what this
        // used to do) copied the caller's operations verbatim but resolved their
        // paths against the entity, so "/Id" and "/DateJoined" became writable --
        // the DTO's whole purpose is to bound which fields a caller may touch.
        var editable = _mapper.Map<UserProfileUpdateDto>(targetUser);

        // The ModelState overload records an invalid path as a validation error;
        // the single-argument one throws JsonPatchException, which meant a typo in
        // "path" answered 500 and left the check below unreachable.
        patch.ApplyTo(editable, ModelState);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // ApplyTo bypasses data annotations, so DisplayName's length and character
        // rules were previously enforced only incidentally, by the column width.
        if (!TryValidateModel(editable))
        {
            return BadRequest(ModelState);
        }

        _mapper.Map(editable, targetUser);

        var success = await _userProfileRepo.UpdateUserProfile(targetUser);

        if (success)
        {
            return Ok(_mapper.Map<UserProfileReadDto>(targetUser));
        }

        return Problem("An error occurred when updating the user profile", title: "Internal Server Error",
                       statusCode: StatusCodes.Status500InternalServerError);
    }
}
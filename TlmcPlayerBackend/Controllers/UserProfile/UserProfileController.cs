using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.UserProfile;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.UserProfile;

[ApiController]
[Route("api/user/profile")]
public class UserProfileController(IUserProfileRepo userProfileRepo) : ControllerBase
{
    private readonly IUserProfileRepo _userProfileRepo = userProfileRepo;

    private UserId CurrentUser => new(User.ToUserClaim().UserId);

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<UserProfileReadDto>> GetMyProfile()
    {
        var profile = await _userProfileRepo.GetProfile(CurrentUser);
        return profile == null ? NotFound() : profile;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileReadDto>> GetProfile(UserId id)
    {
        var profile = await _userProfileRepo.GetProfile(id);
        return profile == null ? NotFound() : profile;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<UserProfileReadDto>> CreateProfile([FromBody] UserProfileWriteDto dto)
    {
        var existing = await _userProfileRepo.GetProfile(CurrentUser);
        if (existing != null)
        {
            return Conflict("Profile already exists");
        }

        var created = await _userProfileRepo.CreateProfile(CurrentUser, dto.DisplayName);
        return created == null ? Conflict("Display name is already taken") : created;
    }

    [HttpPut]
    [Authorize]
    public async Task<ActionResult<UserProfileReadDto>> UpdateProfile([FromBody] UserProfileWriteDto dto)
    {
        var updated = await _userProfileRepo.UpdateProfile(CurrentUser, dto.DisplayName);
        if (updated == null)
        {
            var exists = await _userProfileRepo.GetProfile(CurrentUser) != null;
            return exists ? Conflict("Display name is already taken") : NotFound();
        }

        return updated;
    }
}

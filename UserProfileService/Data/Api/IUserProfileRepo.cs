using UserProfileService.Models;

namespace UserProfileService.Data.Api;

public interface IUserProfileRepo
{
    public Task<UserProfile?> GetUserProfileById(Guid userId);

    public Task<bool> CreateUserProfile(UserProfile userProfile);

    public Task<bool> UpdateUserProfile(UserProfile userProfile);
}
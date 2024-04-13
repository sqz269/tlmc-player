namespace TlmcPlayerBackend.Data.Api.UserProfile;

using TlmcPlayerBackend.Models.UserProfile;

public interface IUserProfileRepo
{
    public Task<UserProfile?> GetUserProfileById(Guid userId);

    public Task<bool> CreateUserProfile(UserProfile userProfile);

    public Task<bool> UpdateUserProfile(UserProfile userProfile);
}
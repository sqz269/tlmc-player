using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data.Api.UserProfile;

namespace TlmcPlayerBackend.Data.Impl.UserProfile;

using TlmcPlayerBackend.Models.UserProfile;

public class UserProfileRepo : IUserProfileRepo
{
    private readonly AppDbContext _dbContext;

    public UserProfileRepo(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserProfile?> GetUserProfileById(Guid userId)
    {
        return await _dbContext.UserProfiles.FirstOrDefaultAsync(d => d.Id == userId);
    }

    public async Task<bool> CreateUserProfile(UserProfile userProfile)
    {
        await _dbContext.UserProfiles.AddAsync(userProfile);
        var itemsSaved = await _dbContext.SaveChangesAsync();
        return itemsSaved >= 1;
    }

    public async Task<bool> UpdateUserProfile(UserProfile userProfile)
    {
        _dbContext.UserProfiles.Update(userProfile);
        var itemsSaved = await _dbContext.SaveChangesAsync();
        return itemsSaved >= 1;
    }
}
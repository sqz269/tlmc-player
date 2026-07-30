using Microsoft.EntityFrameworkCore;
using Npgsql;
using TlmcPlayerBackend.Dtos.UserProfile;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.UserProfile;

namespace TlmcPlayerBackend.Data.Repos;

public interface IUserProfileRepo
{
    Task<UserProfileReadDto?> GetProfile(UserId id);

    /// <summary>Null when the display name is already taken.</summary>
    Task<UserProfileReadDto?> CreateProfile(UserId id, string displayName);

    /// <summary>Null when no profile exists or the display name is taken.</summary>
    Task<UserProfileReadDto?> UpdateProfile(UserId id, string displayName);
}

public class UserProfileRepo(AppDbContext context) : IUserProfileRepo
{
    private readonly AppDbContext _context = context;

    public Task<UserProfileReadDto?> GetProfile(UserId id)
    {
        return _context.UserProfiles
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserProfileReadDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                DateJoined = u.DateJoined,
            })
            .FirstOrDefaultAsync()!;
    }

    public async Task<UserProfileReadDto?> CreateProfile(UserId id, string displayName)
    {
        // The id is the Keycloak `sub`, assigned from the token — never generated.
        _context.UserProfiles.Add(new UserProfile
        {
            Id = id,
            DisplayName = displayName,
            DateJoined = DateTime.UtcNow,
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _context.ChangeTracker.Clear();
            return null;
        }

        return await GetProfile(id);
    }

    public async Task<UserProfileReadDto?> UpdateProfile(UserId id, string displayName)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == id);
        if (profile == null)
        {
            return null;
        }

        profile.DisplayName = displayName;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _context.ChangeTracker.Clear();
            return null;
        }

        return await GetProfile(id);
    }
}

using System.Security.Cryptography;
using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Dtos.UserProfile;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.UserProfile;
using TlmcPlayerBackend.Subsonic;

namespace TlmcPlayerBackend.Controllers.UserProfile;

/// <summary>
/// Management surface for Subsonic-facade API keys (Docs/SUBSONIC.md section 4).
/// Keycloak-authorized, so a first-party client can host a "connect an app" page;
/// the keys themselves are only ever *used* on /rest.
/// </summary>
[ApiController]
[Route("api/user/api-keys")]
[Authorize]
public class ApiKeyController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    private UserId CurrentUser => new(User.ToUserClaim().UserId);

    [HttpGet]
    public async Task<ActionResult<List<ApiKeyReadDto>>> GetKeys()
    {
        var user = CurrentUser;
        return await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == user)
            .OrderBy(k => k.CreatedAt)
            .Select(k => new ApiKeyReadDto
            {
                Id = k.Id,
                Name = k.Name,
                KeyPrefix = k.KeyPrefix,
                CreatedAt = k.CreatedAt,
                LastUsedAt = k.LastUsedAt,
            })
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<ApiKeyCreatedDto>> CreateKey([FromBody] ApiKeyWriteDto dto)
    {
        var user = CurrentUser;

        // api_key.user_id references user_profile, which is created on first login
        // by the client — not implicitly here, where a typo'd token could mint one.
        if (!await _context.UserProfiles.AnyAsync(p => p.Id == user))
        {
            return Conflict("Create a user profile before creating API keys");
        }

        var key = SubsonicApiKey.Generate();
        var row = new ApiKey
        {
            Id = ApiKeyId.New(),
            UserId = user,
            Name = dto.Name,
            KeyHash = SubsonicApiKey.Hash(key),
            KeyPrefix = SubsonicApiKey.DisplayPrefix(key),
        };

        _context.ApiKeys.Add(row);
        await _context.SaveChangesAsync();

        return new ApiKeyCreatedDto
        {
            Id = row.Id,
            Name = row.Name,
            KeyPrefix = row.KeyPrefix,
            CreatedAt = row.CreatedAt,
            Key = key,
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteKey(ApiKeyId id)
    {
        var user = CurrentUser;
        var deleted = await _context.ApiKeys
            .Where(k => k.Id == id && k.UserId == user)
            .ExecuteDeleteAsync();

        return deleted == 0 ? NotFound() : NoContent();
    }
}

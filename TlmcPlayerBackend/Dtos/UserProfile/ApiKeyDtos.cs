using System.ComponentModel.DataAnnotations;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Dtos.UserProfile;

public class ApiKeyWriteDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = null!;
}

public class ApiKeyReadDto
{
    public ApiKeyId Id { get; set; }
    public string Name { get; set; } = null!;
    public string KeyPrefix { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>Returned only from creation: the one time the key itself exists on the wire.</summary>
public class ApiKeyCreatedDto : ApiKeyReadDto
{
    public string Key { get; set; } = null!;
}

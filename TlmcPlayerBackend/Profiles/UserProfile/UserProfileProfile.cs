using AutoMapper;
using TlmcPlayerBackend.Dtos.UserProfile;

namespace TlmcPlayerBackend.Profiles.UserProfile;
using TlmcPlayerBackend.Models.UserProfile;

public class UserProfileProfile : Profile
{
    public UserProfileProfile()
    {
        CreateMap<UserProfile, UserProfileReadDto>();
        CreateMap<UserProfileWriteDto, UserProfile>();

        // Round trip for patching: project the entity onto the update DTO, apply the
        // patch to that, then map the result back. Since DisplayName is the only
        // member of the DTO, this is what stops a patch reaching Id or DateJoined.
        // The JsonPatchDocument/Operation maps that used to live here did the
        // opposite -- they retyped the caller's document to the entity.
        CreateMap<UserProfile, UserProfileUpdateDto>();
        CreateMap<UserProfileUpdateDto, UserProfile>();
    }
}

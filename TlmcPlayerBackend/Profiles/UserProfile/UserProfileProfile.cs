using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using TlmcPlayerBackend.Dtos.UserProfile;

namespace TlmcPlayerBackend.Profiles.UserProfile;
using TlmcPlayerBackend.Models.UserProfile;

public class UserProfileProfile : Profile
{
    public UserProfileProfile() 
    {
        CreateMap<UserProfile, UserProfileReadDto>();
        CreateMap<UserProfileWriteDto, UserProfile>();
        CreateMap<JsonPatchDocument<UserProfileUpdateDto>, JsonPatchDocument<UserProfile>>();
        CreateMap<Operation<UserProfileUpdateDto>, Operation<UserProfile>>();
    }
}
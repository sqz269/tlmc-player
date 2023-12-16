using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using UserProfileService.Dtos;
using UserProfileService.Models;

namespace UserProfileService.Profiles;

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
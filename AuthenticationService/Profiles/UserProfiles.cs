using AuthenticationService.Dtos;
using AuthenticationService.Models.Db;
using AutoMapper;

namespace AuthenticationService.Profiles;

public class UserProfiles : Profile
{
    public UserProfiles()
    {
        CreateMap<UserCredentialsDto, User>();
    }
}
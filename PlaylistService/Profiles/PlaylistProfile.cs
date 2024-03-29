﻿using AutoMapper;
using PlaylistService.Dtos;
using PlaylistService.Model;

namespace PlaylistService.Profiles;

public class PlaylistProfile : Profile
{
    public PlaylistProfile()
    {
        CreateMap<Playlist, PlaylistReadDto>();
    }
}
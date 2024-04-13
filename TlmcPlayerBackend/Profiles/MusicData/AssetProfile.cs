using AutoMapper;
using TlmcPlayerBackend.Controllers.MusicData;
using TlmcPlayerBackend.Dtos.MusicData.Asset;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

public class AssetUrlMapper : IMemberValueResolver<Asset, AssetReadDto, Guid, string>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _contextAccessor;

    public AssetUrlMapper(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _contextAccessor = httpContextAccessor;
    }

    public string Resolve(Asset source, AssetReadDto destination, Guid sourceMember, string destMember, ResolutionContext context)
    {
        return _linkGenerator.GetUriByName(_contextAccessor.HttpContext,
            nameof(AssetController.GetAsset),
            new { Id = sourceMember },
            fragment: FragmentString.Empty);
    }
}

public class AssetProfile : Profile
{
    public AssetProfile()
    {
        CreateMap<Asset, AssetReadDto>()
            .ForMember(a => a.Url,
                t => t.MapFrom<AssetUrlMapper, Guid>(asset => asset.Id));

        CreateMap<AssetWriteDto, Asset>();
    }
}
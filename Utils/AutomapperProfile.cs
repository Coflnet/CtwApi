using AutoMapper;
using Coflnet.Auth;

public class AutomapperProfile : Profile
{
    public AutomapperProfile()
    {
        CreateMap<PrivacyService.InternalConsentData, ConsentData>().ReverseMap();
    }
}
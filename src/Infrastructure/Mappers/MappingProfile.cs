using AutoMapper;
using Core.Models;
using Domain.Entities;

namespace Infrastructure.Mappers;

public class MappingProfile: Profile
{
    public MappingProfile()
    {
        // Маппинг из Entity в Domain Model
        CreateMap<BackgroundTaskEntity, BackgroundTask>()
            .ForMember(dest => dest.TaskData, opt => 
                opt.MapFrom(src => src.TaskData))
            .ForMember(dest => dest.Result, opt => 
                opt.MapFrom(src => src.Result));
            
        // Маппинг из Domain Model в Entity
        CreateMap<BackgroundTask, BackgroundTaskEntity>()
            .ForMember(dest => dest.TaskData, opt => 
                opt.MapFrom(src => src.TaskData))
            .ForMember(dest => dest.Result, opt => 
                opt.MapFrom(src => src.Result));
    }
}
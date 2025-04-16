using AutoMapper;
using Domain.Entities;

namespace Infrastructure.Mappers;

public class ImportColumnMetadataProfile: Profile
{
    public ImportColumnMetadataProfile()
    {
        CreateMap<ImportColumnMetadataEntity, ImportColumnMetadataProfile>().ReverseMap();
    }
}
using Domain.Entities;

namespace Domain.RepoInterfaces;

public interface IImportColumnMetadataRepository: IRepository<ImportColumnMetadataEntity>
{
    Task<List<ImportColumnMetadataEntity>> GetByTableNameAsync(string tableName);
}
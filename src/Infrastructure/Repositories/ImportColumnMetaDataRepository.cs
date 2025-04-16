using Domain.Entities;
using Domain.RepoInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ImportColumnMetaDataRepository: IImportColumnMetadataRepository
{
    private readonly Context _context;

    public ImportColumnMetaDataRepository(Context context)
    {
        _context = context;
    }
    public async Task<IEnumerable<ImportColumnMetadataEntity>> GetAllAsync()
    {
        return await _context.ImportColumnMetadatas.ToListAsync();
    }

    public async Task<ImportColumnMetadataEntity> GetByIdAsync(int id)
    {
        return await _context.ImportColumnMetadatas.FindAsync(id); 
    }

    public async Task<bool> Add(ImportColumnMetadataEntity entity)
    {
        return await _context.ImportColumnMetadatas.AddAsync(entity) != null; 
    }

    public async Task<bool> Update(ImportColumnMetadataEntity entity)
    {
        _context.ImportColumnMetadatas.Update(entity);
        return true;
    }

    public async Task<bool> Delete(int id)
    {
        return _context.ImportColumnMetadatas.Remove(await GetByIdAsync(id)) != null;
        return true;
    }

    public async Task<List<ImportColumnMetadataEntity>> GetByTableNameAsync(string tableName)
    {
        return await _context.ImportColumnMetadatas.Where(x => x.TableName == tableName).ToListAsync();
    }
}
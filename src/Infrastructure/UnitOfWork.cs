using Core;
using Domain.RepoInterfaces;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class UnitOfWork: IUnitOfWork
{
    private readonly Context _context;
    private IUserRepository _userRepository;
    private IRefreshTokenRepository _refreshTokenRepository;
    private IBackgroundTaskRepository _backgroundTaskRepository;
    private IImportColumnMetadataRepository _columnMetadataRepository;
    
    public UnitOfWork(Context context)
    {
        _context = context;
    }
    
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
    
    public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sqlQuery)
    {
        return await _context.Database.SqlQueryRaw<T>(sqlQuery).ToListAsync();
    }
    public async Task<T> ExecuteScalarAsync<T>(string sqlQuery)
    {
        var result = await _context.Database.SqlQueryRaw<T>(sqlQuery).FirstOrDefaultAsync();
        return result;
    }

    /// <summary>
    /// Репозиторий для работы с пользователями
    /// </summary>
    public IUserRepository Users => 
        _userRepository ??= new UserRepository(_context);

    /// <summary>
    /// Репозиторий для работы с refresh токенами
    /// </summary>
    public IRefreshTokenRepository RefreshTokens => 
        _refreshTokenRepository ??= new RefreshTokenRepository(_context);
    
    /// <summary>
    /// Репозиторий для работы с фоновыми задачами
    /// </summary>
    public IBackgroundTaskRepository BackgroundTasks =>
        _backgroundTaskRepository ??= new BackgroundTaskRepository(_context);
    
    /// <summary>
    /// Репозиторий для работы с фоновыми задачами
    /// </summary>
    public IImportColumnMetadataRepository ImportColumnMetadatas =>
        _columnMetadataRepository ??= new ImportColumnMetaDataRepository(_context);
    public async Task<bool> CommitAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
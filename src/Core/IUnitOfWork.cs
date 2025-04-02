using Domain.RepoInterfaces;

namespace Core;

public interface IUnitOfWork: IDisposable
{
    Task SaveChangesAsync();
    Task<IEnumerable<T>> ExecuteQueryAsync<T>(string query);
    Task<T> ExecuteScalarAsync<T>(string sqlQuery);
    IUserRepository Users { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    Task<bool> CommitAsync();
}
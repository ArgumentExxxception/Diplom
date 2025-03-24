using System.Collections;

namespace Core;

public interface IUnitOfWork: IDisposable
{
    Task SaveChangesAsync();
    Task<IEnumerable<T>> ExecuteQueryAsync<T>(string query);
    Task<T> ExecuteScalarAsync<T>(string sqlQuery);
}
namespace Domain.RepoInterfaces;

public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> GetByIdAsync(int id);
    Task<bool> Add(T entity);
    Task<bool> Update(T entity);
    Task<bool> Delete(int id);
}
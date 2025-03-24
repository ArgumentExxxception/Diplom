using Core;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class Repository<TEntity>: IRepository<TEntity> where TEntity : class
{
    public readonly Context _context;
    private readonly DbSet<TEntity> _dbSet;

    public Repository(Context context, DbSet<TEntity> dbSet)
    {
        _context = context;
        _dbSet = dbSet;
    }
    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<TEntity> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public void Add(TEntity entity)
    {
        _dbSet.Add(entity);
    }

    public void Update(TEntity entity)
    {
        _dbSet.Attach(entity);
        _context.Entry(entity).State = EntityState.Modified;
    }
    
    public void Delete(TEntity entity)
    {
        if (_context.Entry(entity).State == EntityState.Detached)
        {
            _dbSet.Attach(entity);
        }
        _dbSet.Remove(entity);
    }
}
using Core;
using Domain.RepoInterfaces;
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

    public async Task<bool> Add(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
        return true;
    }

    public async Task<bool> Update(TEntity entity)
    {
        _dbSet.Update(entity);
        _context.Entry(entity).State = EntityState.Modified;
        return true;
    }
    
    public async Task<bool> Delete(int id)
    {
        var user = await GetByIdAsync(id);
        if (user == null) return false;

        _dbSet.Remove(user);
        return true;
    }
}
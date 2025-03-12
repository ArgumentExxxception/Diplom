using Core;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class UnitOfWork: IUnitOfWork
{
    private readonly Context _context;
    
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

    public void Dispose()
    {
        _context.Dispose();
    }
}
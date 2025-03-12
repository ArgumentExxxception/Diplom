using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class Context: DbContext
{
    public Context(DbContextOptions<Context> options): base(options)
    {
        
    }
}
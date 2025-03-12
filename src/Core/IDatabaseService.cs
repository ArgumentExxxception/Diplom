namespace Core;

public interface IDatabaseService
{
    Task<IEnumerable<string>> GetPublicTablesAsync();
}
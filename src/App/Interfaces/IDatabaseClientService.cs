namespace BlazorApp1.Interfaces;

public interface IDatabaseClientService
{
    Task<IEnumerable<string>> GetPublicTablesAsync();
}
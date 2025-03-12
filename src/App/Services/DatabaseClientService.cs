using BlazorApp1.Interfaces;

namespace BlazorApp1.Services;

public class DatabaseClientService : IDatabaseClientService
{
    private readonly HttpClient _httpClient;

    public DatabaseClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<string>> GetPublicTablesAsync()
    {
        try
        {
            // Отправляем GET-запрос к API
            var response = await _httpClient.GetAsync("/api/Tabels/public");
            response.EnsureSuccessStatusCode(); // Проверка успешного статуса

            var tables = await response.Content.ReadFromJsonAsync<List<string>>();
            return tables ?? new List<string>();
        }
        catch (HttpRequestException ex)
        {
            // Обработка ошибок (например, логирование)
            Console.WriteLine($"Error fetching public tables: {ex.Message}");
            throw;
        }
    }
}
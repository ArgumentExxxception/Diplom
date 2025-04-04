using App.Interfaces;
using Core.Models;

namespace App.Services;

public class DatabaseClientService : IDatabaseClientService
{
    private readonly HttpClient _httpClient;

    public DatabaseClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TableModel>> GetPublicTablesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:5056/api/Tables/public");
        
            if (!response.IsSuccessStatusCode)
            {
                return new List<TableModel>();
            }
        
            return await response.Content.ReadFromJsonAsync<List<TableModel>>() ?? new List<TableModel>();
        }
        catch (Exception ex)
        {
            // Логирование ошибки
            Console.WriteLine($"Ошибка при получении публичных таблиц: {ex.Message}");
            return new List<TableModel>();
        }
    }
    
    public async Task<string> CreateTablesAsync(TableModel tableModel)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Tables/create", tableModel);
        
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        
            // Чтение сообщения об ошибке из ответа
            var errorMessage = await response.Content.ReadAsStringAsync();
            return $"Ошибка: {errorMessage}";
        }
        catch (Exception ex)
        {
            return $"Ошибка при создании таблицы: {ex.Message}";
        }
    }
}
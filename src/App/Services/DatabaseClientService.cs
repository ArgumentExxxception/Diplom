using System.Text.Json;
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
            var tables = await _httpClient.GetFromJsonAsync<List<TableModel>>("api/Tabels/public");
            return tables;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching public tables: {ex.Message}");
            throw;
        }
    }
    
    public async Task<string> CreateTablesAsync(TableModel tableModel)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(tableModel, options);
            var response = await _httpClient.PostAsJsonAsync(
                "api/Tabels/create", 
                tableModel);

            return await HandleResponse(response);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching public tables: {ex.Message}");
            throw;
        }
    }
    
    private async Task<string> HandleResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
            
        var error = await response.Content.ReadAsStringAsync();
        return $"Error ({response.StatusCode}): {error}";
    }
}
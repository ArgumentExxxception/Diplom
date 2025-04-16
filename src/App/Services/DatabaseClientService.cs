using App.Interfaces;
using Blazored.LocalStorage;
using Core.Models;
using Domain.Entities;

namespace App.Services;

public class DatabaseClientService : HttpClientBase,IDatabaseClientService
{
    public DatabaseClientService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        ErrorHandlingService errorHandler) 
        : base(httpClient, localStorage, errorHandler)
    {
    }

    public async Task<List<TableModel>> GetPublicTablesAsync()
    {
        try
        {
            return await GetAsync<List<TableModel>>("api/tables/public");
        }
        catch
        {
            // Ошибка уже обработана в базовом классе HttpClientBase
            return new List<TableModel>();
        }
    }

    public async Task<List<ImportColumnsMetadataModel>> GetTablesMetadataAsync()
    {
        try
        {
            return await GetAsync<List<ImportColumnsMetadataModel>>("api/tables/metadata");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<string> CreateTablesAsync(TableModel tableModel)
    {
        try
        {
            return await PostAsync<string>("api/tables/create", tableModel);
        }
        catch (Exception ex)
        {
            // Ошибка уже обработана в базовом классе
            return $"Ошибка при создании таблицы: {ex.Message}";
        }
    }
}
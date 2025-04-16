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
}
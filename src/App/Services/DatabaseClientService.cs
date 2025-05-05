using App.Interfaces;
using Blazored.LocalStorage;
using Core.Models;
using Domain.Entities;

namespace App.Services;

public class DatabaseClientService : HttpClientBase,IDatabaseClientService
{
    public DatabaseClientService(
        HttpClient httpClient,
        ErrorHandlingService errorHandler) 
        : base(httpClient, errorHandler)
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
            return new List<TableModel>();
        }
    }
}
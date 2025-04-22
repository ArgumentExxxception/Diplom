using System.Net.Http.Headers;
using System.Text;
using App.Interfaces;
using Blazored.LocalStorage;
using Core.Models;
using Microsoft.JSInterop;

namespace App.Services;

public class DataExportClientService: HttpClientBase,IDataExportClientService
{
    private readonly IJSRuntime _jsRuntime;

    public DataExportClientService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        ErrorHandlingService errorHandler,
        IJSRuntime jsRuntime)
        : base(httpClient, localStorage, errorHandler)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> ExportDataAsync(TableExportRequestModel exportRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            await SetAuthHeaderAsync();

            var response = await _httpClient.PostAsJsonAsync("api/File/export", exportRequest, cancellationToken);
    
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Ошибка при экспорте: {response.StatusCode}. {errorContent}");
            }
    
            var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        
            // По умолчанию используем формат из запроса или CSV если формат не указан
            string fileExtension = !string.IsNullOrEmpty(exportRequest.ExportFormat) 
                ? exportRequest.ExportFormat.ToLowerInvariant() 
                : "csv";
        
            string fileName = $"export.{fileExtension}";
        
            if (response.Headers.TryGetValues("Content-Disposition", out var contentDisposition))
            {
                var contentDispositionValue = ContentDispositionHeaderValue.Parse(contentDisposition.First());
                fileName = contentDispositionValue.FileName?.Trim('"') ?? fileName;
            }
        
            await _jsRuntime.InvokeVoidAsync("saveAsFile", cancellationToken, fileName, GetContentType(fileName), Convert.ToBase64String(fileBytes));
    
            return fileName;
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            throw;
        }
    }

    public async Task<long> GetExportDataSizeAsync(string tableName, string filterCondition = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await SetAuthHeaderAsync();
            
            string url = $"api/File/export-size?tableName={Uri.EscapeDataString(tableName)}";
            if (!string.IsNullOrEmpty(filterCondition))
            {
                url += $"&filterCondition={Uri.EscapeDataString(filterCondition)}";
            }
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            var size = await response.Content.ReadFromJsonAsync<long>(cancellationToken: cancellationToken);
            return size;
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            return 0;
        }
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => "text/csv",
            ".xml" => "application/xml",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
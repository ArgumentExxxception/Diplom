using System.Text;
using BlazorApp1.Components.Dialogs;
using BlazorApp1.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace BlazorApp1.Components.Pages;

public partial class FiluUploadPage : ComponentBase
{
    [Inject] IDialogService DialogService { get; set; }
    [Inject] private IDatabaseClientService _databaseClientService { get; set; }
    private string FileName { get; set; }
    private string ErrorMessage { get; set; }
    IReadOnlyList<IBrowserFile> _files = new List<IBrowserFile>();

    public FiluUploadPage(IDatabaseClientService databaseClientService)
    {
        _databaseClientService = databaseClientService;
    }
    
    private void OnFileChange(IReadOnlyList<IBrowserFile>? files)
    {
        _files= files;
    }
    
    
    
    public async Task ShowDialog(InputFileChangeEventArgs e)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<FileUploadingDialogContent>("Выберите действие", options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            var selectedAction = result.Data as string;
            if (selectedAction == "Вставить в новую")
            {
                // Запрос списка таблиц с сервера
                var tableNames = await _databaseClientService.GetPublicTablesAsync();
            }
            else if (selectedAction == "Вставить в текущую")
            {
                // Действие для вставки в текущую таблицу
            }
        }
    }

    private async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        try
        {
            // Очистка предыдущих сообщений
            FileName = string.Empty;
            ErrorMessage = string.Empty;

            // Получение выбранного файла
            var file = e.File;

            // Проверка расширения файла
            if (file.Name.EndsWith(".csv") || file.Name.EndsWith(".xml"))
            {
                FileName = file.Name;

                // Здесь можно добавить логику для обработки файла
                // Например, чтение содержимого и передача в CsvDataLoader
                using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
                {
                    var fileContent = await reader.ReadToEndAsync();
                }
                // await ProcessFileAsync(fileContent); // Ваш метод для обработки файла

                // Уведомление об успешной загрузке
                Snackbar.Add($"Файл {FileName} успешно загружен.", Severity.Success);
            }
            else
            {
                ErrorMessage = "Неподдерживаемый формат файла. Выберите файл с расширением .csv или .xml.";
                Snackbar.Add(ErrorMessage, Severity.Error);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка при загрузке файла: {ex.Message}";
            Snackbar.Add(ErrorMessage, Severity.Error);
        }
    }
}
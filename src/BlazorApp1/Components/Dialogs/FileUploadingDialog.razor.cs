using BlazorApp1.Interfaces;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorApp1.Components.Dialogs;

public partial class FileUploadingDialog : ComponentBase
{
    [Inject] private IDatabaseClientService _databaseClientService { get; set; }

    public FileUploadingDialog(IDatabaseClientService databaseClientService)
    {
        _databaseClientService = databaseClientService;
    }
    protected override Task OnInitializedAsync()
    {
        return base.OnInitializedAsync();
    }
    
    private async Task ShowDialog()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = DialogService.Show<FileUploadingDialogContent>("Выберите действие", options);
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
}
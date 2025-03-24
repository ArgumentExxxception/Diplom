using App.Components.Dialogs;
using App.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace App.Components.Pages;

public partial class Test : ComponentBase
{
    [Inject] private IDialogService _dialogService { get; set; }
    [Inject] private IDatabaseClientService _databaseClientService { get; set; }

    public Test(IDatabaseClientService databaseClientService, IDialogService dialogService)
    {
        _dialogService = dialogService;
        _databaseClientService = databaseClientService;
    }
    
    public async Task ShowDialog(InputFileChangeEventArgs e)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await _dialogService.ShowAsync<ChoiceDialogResult>("Выберите действие", options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            var selectedAction = result.Data as string;
            if (selectedAction == "New")
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
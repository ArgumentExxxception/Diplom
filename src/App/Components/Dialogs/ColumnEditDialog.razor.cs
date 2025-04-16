using Core.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Dialogs;

public partial class ColumnEditDialog : ComponentBase
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }
    [Parameter] public ColumnInfo Column { get; set; } = new();

    private bool IsValid => !string.IsNullOrWhiteSpace(Column.Name);

    private IEnumerable<string> ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield return "Введите название колонки";
    }

    private void Cancel() => MudDialog.Cancel();
    private void Save() => MudDialog.Close(DialogResult.Ok(Column));
}
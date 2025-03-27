using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Dialogs;

public partial class ConfirmDialog : ComponentBase
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    [Parameter] public string ContentText { get; set; }
    [Parameter] public string ButtonText { get; set; }
    [Parameter] public Color Color { get; set; }

    private void OnCancel()
    {
        MudDialog.Cancel();
    }

    private void OnConfirm()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }
}
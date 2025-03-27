using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Dialogs;

public partial class DuplicateDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; }
    
    [Parameter] public List<Dictionary<string, object>> Duplicates { get; set; } = new();

    private void OnOverwriteClicked()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void OnSkipClicked()
    {
        MudDialog.Close(DialogResult.Ok(false));
    }
}
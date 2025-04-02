using Core.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Dialogs;

public partial class ImportProgressDialog : ComponentBase
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }
    
    [Parameter] public ImportProgressInfo Progress { get; set; } = new ImportProgressInfo();
    [Parameter] public CancellationTokenSource CancellationTokenSource { get; set; }
    
    private bool IsCancellationRequested => CancellationTokenSource?.IsCancellationRequested ?? false;
    
    private void CloseDialog()
    {
        MudDialog.Close(DialogResult.Ok(Progress));
    }
    
    private void CancelImport()
    {
        CancellationTokenSource?.Cancel();
    }
    
    public void Update(ImportProgressInfo progress)
    {
        Progress = progress;
        StateHasChanged();
    }
}
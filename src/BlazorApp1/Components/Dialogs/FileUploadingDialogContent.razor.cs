using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorApp1.Components.Dialogs;

public partial class FileUploadingDialogContent : ComponentBase
{
    [Inject] private IDialogService DialogService { get; set; }
    [CascadingParameter] 
    private MudDialogInstance MudDialog { get; set; }

    private string SelectedAction { get; set; } = "Вставить в текущую базу данных";

    private void Cancel() => MudDialog.Cancel();
    private void Submit() => MudDialog.Close(DialogResult.Ok(SelectedAction));

}
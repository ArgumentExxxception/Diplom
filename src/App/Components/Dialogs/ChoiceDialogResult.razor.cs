using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Dialogs;

public partial class ChoiceDialogResult
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; }

    [Parameter] public string ContentText { get; set; }

    private void InsertIntoNew() =>
        MudDialog.Close(DialogResult.Ok("New"));

    private void InsertIntoCurrent() => 
        MudDialog.Close(DialogResult.Ok("Current"));
}
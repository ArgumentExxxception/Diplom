using App.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Shared;

public partial class ErrorBoundary : ComponentBase
{
    [Parameter] public RenderFragment ChildContent { get; set; }
    
    [Parameter] public RenderFragment<Exception> ErrorContent { get; set; }
    
    [Inject] private ErrorHandlingService ErrorHandler { get; set; }
    
    public Exception CurrentException { get; private set; }
    
    protected override void OnInitialized()
    {
        if (ErrorContent == null)
        {
            ErrorContent = DefaultErrorContent;
        }
    }
    
    public void ProcessError(Exception exception)
    {
        CurrentException = exception;
        
        Console.Error.WriteLine($"Ошибка: {exception.Message}");

        ErrorHandler.HandleException(exception);
        
        StateHasChanged();
    }

    private RenderFragment<Exception> DefaultErrorContent => (exception) => builder => builder.AddContent(1,
        "<MudAlert Severity=\"Severity.Error\" Class=\"my-4\"> <MudText Typo=\"Typo.subtitle1\"><strong>Произошла ошибка:</strong></MudText>" +
        "<MudText Typo=\"Typo.body2\">@exception.Message</MudText> " +
        "<MudButton OnClick=\"ResetError\" Variant=\"Variant.Outlined\" Color=\"Color.Primary\" Size=\"Size.Small\" Class=\"mt-2\">Перезагрузить страницу</MudButton></MudAlert>;");
    
    public void ResetError()
    {
        CurrentException = null;
        StateHasChanged();
    }
}
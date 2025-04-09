using App.Interfaces;
using App.Services;
using Core.Entities;
using Core.Validators;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Pages;

public partial class Login: ComponentBase
{
    [Inject] private IAuthClientService AuthService { get; set; }
    [Inject] private NavigationManager NavigationManager { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private ErrorHandlingService ErrorHandler { get; set; }

    private LoginRequestDto loginModel = new LoginRequestDto();
    private bool loading = false;
    private string error = string.Empty;
    private LoginRequestValidator _validator = new LoginRequestValidator();

    private async Task HandleLogin()
    {
        loading = true;
        error = string.Empty;

        try
        {
            var result = await AuthService.Login(loginModel);
                
            if (result.Successful)
            {
                Snackbar.Add("Успешный вход", Severity.Success);
                NavigationManager.NavigateTo("/");
            }
            else
            {
                error = result.Error;
                ErrorHandler.ShowErrorMessage(error);
            }
        }
        catch (Exception ex)
        {
            error = "Произошла ошибка при входе";
            ErrorHandler.HandleException(ex);
        }
        finally
        {
            loading = false;
        }
    }
}
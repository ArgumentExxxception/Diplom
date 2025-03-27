using App.Interfaces;
using App.Services;
using Blazored.FluentValidation;
using Core.Entities;
using Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace App.Components.Pages;

public partial class Login: ComponentBase
{
    
    [Inject] private IAuthClientService AuthService { get; set; }
    [Inject] private NavigationManager NavigationManager { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }

    private LoginRequestDto loginModel = new LoginRequestDto();
    private bool loading = false;
    private string error = string.Empty;
    private bool formValid = false;

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
                Snackbar.Add(error, Severity.Error);
            }
        }
        catch (Exception ex)
        {
            error = "Произошла ошибка при входе: " + ex.Message;
            Snackbar.Add(error, Severity.Error);
        }
        finally
        {
            loading = false;
        }
    }
}
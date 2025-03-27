using System.Text.RegularExpressions;
using App.Interfaces;
using Core.DTOs;
using Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Pages;

public partial class Register : ComponentBase
{
    [Inject] private IAuthClientService _authClientService { get; set; }
    [Inject] private ISnackbar _snackbar { get; set; }
    [Inject] private NavigationManager _navigationManager { get; set; }
    private RegisterRequestDto registerModel = new RegisterRequestDto();
    private bool isFormValid;
    private bool isProcessing = false;
    private string errorMessage = string.Empty;
    private bool showPassword = false;
    private bool showConfirmPassword = false;
    private MudForm form;

    private IEnumerable<string> ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            yield return "Имя пользователя обязательно";

        if (username?.Length < 3)
            yield return "Имя пользователя должно содержать минимум 3 символа";

        if (username != null && !Regex.IsMatch(username, @"^[a-zA-Z0-9]+$"))
            yield return "Имя пользователя может содержать только буквы и цифры";
    }

    private string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "Email обязателен";

        var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        if (!regex.IsMatch(email))
            return "Введите корректный email";

        return null;
    }

    private IEnumerable<string> ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            yield return "Пароль обязателен";

        if (password?.Length < 8)
            yield return "Пароль должен содержать минимум 8 символов";

        if (password != null && !Regex.IsMatch(password, @"[A-Za-z]"))
            yield return "Пароль должен содержать хотя бы одну букву";

        if (password != null && !Regex.IsMatch(password, @"[0-9]"))
            yield return "Пароль должен содержать хотя бы одну цифру";
    }

    private string ValidateConfirmPassword(string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(confirmPassword))
            return "Подтверждение пароля обязательно";

        if (confirmPassword != registerModel.Password)
            return "Пароли не совпадают";

        return null;
    }

    private async Task HandleRegister()
    {
        await form.Validate();

        if (!isFormValid)
            return;

        isProcessing = true;
        errorMessage = string.Empty;

        try
        {
            var result = await _authClientService.Register(registerModel);
            
            if (result.Successful)
            {
                _snackbar.Add("Регистрация успешна! Выполнен вход в систему.", Severity.Success);
                _navigationManager.NavigateTo("/");
            }
            else
            {
                errorMessage = result.Error;
                _snackbar.Add(errorMessage, Severity.Error);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Произошла ошибка при регистрации: {ex.Message}";
            _snackbar.Add(errorMessage, Severity.Error);
        }
        finally
        {
            isProcessing = false;
        }
    }
}
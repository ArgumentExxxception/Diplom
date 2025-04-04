using System.Net.Http.Headers;
using App.Interfaces;
using Blazored.LocalStorage;
using Core.Commands;
using Core.DTOs;
using Core.Entities;
using Core.Queries;
using Core.Results;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;

namespace App.Services;

public class AuthClientService : IAuthClientService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILocalStorageService _localStorage;

    public AuthClientService(
        HttpClient httpClient,
        AuthenticationStateProvider authStateProvider,
        ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
        _localStorage = localStorage;
    }

    /// <summary>
    /// Выполняет вход пользователя
    /// </summary>
    public async Task<LoginResponse> Login(LoginRequestDto loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
        var loginResult = await response.Content.ReadFromJsonAsync<LoginResponse>();

        if (loginResult.Successful)
        {
            await _localStorage.SetItemAsync("authToken", loginResult.Token);
            await _localStorage.SetItemAsync("refreshToken", loginResult.RefreshToken);
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(loginResult.Token);
        }

        return loginResult;
    }

    /// <summary>
    /// Регистрирует нового пользователя
    /// </summary>
    public async Task<LoginResponse> Register(RegisterRequestDto registerRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task Logout()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (!string.IsNullOrEmpty(token))
        {
            // Добавляем токен в заголовок запроса
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await _httpClient.PostAsync("api/auth/logout", null);
        }

        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
    }

    
    public async Task<LoginResponse> RefreshToken()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refreshToken))
                return new LoginResponse { Successful = false, Error = "Отсутствуют токены" };

            var refreshRequest = new RefreshTokenRequestDto
            {
                Token = token,
                RefreshToken = refreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("api/auth/refresh-token", refreshRequest);
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

            if (result.Successful)
            {
                await _localStorage.SetItemAsync("authToken", result.Token);
                await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
            }

            return result;
        }
        catch (Exception ex)
        {
            return new LoginResponse { Successful = false, Error = $"Ошибка при обновлении токена: {ex.Message}" };
        }
    }

    /// <summary>
    /// Проверяет, авторизован ли пользователь
    /// </summary>
    public async Task<bool> IsUserAuthenticated()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User.Identity.IsAuthenticated;
    }

    /// <summary>
    /// Получает текущий JWT токен
    /// </summary>
    public async Task<string> GetToken()
    {
        return await _localStorage.GetItemAsync<string>("authToken");
    }
}
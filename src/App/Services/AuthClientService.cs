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

public class AuthClientService : HttpClientBase ,IAuthClientService
{
    private readonly AuthenticationStateProvider _authStateProvider;

    public AuthClientService(
        HttpClient httpClient,
        AuthenticationStateProvider authStateProvider,
        ILocalStorageService localStorage,
        ErrorHandlingService errorHandler)
        : base(httpClient, localStorage, errorHandler)
    {
        _authStateProvider = authStateProvider;
    }

    /// <summary>
    /// Выполняет вход пользователя
    /// </summary>
    public async Task<LoginResponse> Login(LoginRequestDto loginRequest)
    {
        try
        {
            var response = await PostAsync<LoginResponse>("api/auth/login", loginRequest);
            
            if (response.Successful)
            {
                await _localStorage.SetItemAsync("authToken", response.Token);
                await _localStorage.SetItemAsync("refreshToken", response.RefreshToken);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(response.Token);
            }
            
            return response;
        }
        catch (Exception)
        {
            // Ошибка уже обработана в базовом классе HttpClientBase
            return new LoginResponse { Successful = false, Error = "Не удалось выполнить вход" };
        }
    }

    /// <summary>
    /// Регистрирует нового пользователя
    /// </summary>
    public async Task<LoginResponse> Register(RegisterRequestDto registerRequest)
    {
        try
        {
            var response = await PostAsync<LoginResponse>("api/auth/register", registerRequest);
            
            if (response.Successful)
            {
                await _localStorage.SetItemAsync("authToken", response.Token);
                await _localStorage.SetItemAsync("refreshToken", response.RefreshToken);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(response.Token);
            }
            
            return response;
        }
        catch (Exception)
        {
            // Ошибка уже обработана в базовом классе HttpClientBase
            return new LoginResponse { Successful = false, Error = "Не удалось выполнить регистрацию" };
        }
    }
    /// <summary>
    /// Выполняет выход пользователя
    /// </summary>
    public async Task Logout()
    {
        try
        {
            await PostAsync("api/auth/logout");
            await _localStorage.RemoveItemAsync("authToken");
            await _localStorage.RemoveItemAsync("refreshToken");
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        }
        catch (Exception)
        {
            // Даже в случае ошибки, мы все равно удаляем токены и выходим из системы
            await _localStorage.RemoveItemAsync("authToken");
            await _localStorage.RemoveItemAsync("refreshToken");
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        }
    }

    /// Обновляет токен
    /// </summary>
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

            var response = await PostAsync<LoginResponse>("api/auth/refresh-token", refreshRequest);
            
            if (response.Successful)
            {
                await _localStorage.SetItemAsync("authToken", response.Token);
                await _localStorage.SetItemAsync("refreshToken", response.RefreshToken);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(response.Token);
            }

            return response;
        }
        catch (Exception)
        {
            // Ошибка уже обработана в базовом классе HttpClientBase
            return new LoginResponse { Successful = false, Error = "Не удалось обновить токен" };
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
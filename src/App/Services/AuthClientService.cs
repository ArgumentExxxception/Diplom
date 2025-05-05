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
        ErrorHandlingService errorHandler)
        : base(httpClient, errorHandler)
    {
        _authStateProvider = authStateProvider;
    }

    public async Task<LoginResponse> Login(LoginRequestDto loginRequest)
    {
        try
        {
            var response = await PostAsync<UserDto>("api/auth/login", loginRequest);
            
            if (response != null)
            {
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication();
                return new LoginResponse { Successful = true, User = response };
            }
            
            return new LoginResponse { Successful = false, Error = "Не удалось выполнить вход" };
        }
        catch (Exception ex)
        {
            return new LoginResponse { Successful = false, Error = "Не удалось выполнить вход: " + ex.Message };
        }
    }

    public async Task<LoginResponse> Register(RegisterRequestDto registerRequest)
    {
        try
        {
            var response = await PostAsync<UserDto>("api/auth/register", registerRequest);
            
            if (response != null)
            {
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication();
                return new LoginResponse { Successful = true, User = response };
            }
            
            return new LoginResponse { Successful = false, Error = "Не удалось выполнить регистрацию" };
        }
        catch (Exception ex)
        {
            return new LoginResponse { Successful = false, Error = "Не удалось выполнить регистрацию: " + ex.Message };
        }
    }

    public async Task Logout()
    {
        try
        {
            await PostAsync("api/auth/logout");
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
        }
    }

    public async Task Test()
    {
        await GetAsync<string?>("api/auth/whoami");
    }
}
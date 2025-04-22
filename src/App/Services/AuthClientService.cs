using App.Interfaces;
using Core.DTOs;
using Core.Entities;
using Core.Results;

namespace App.Services;

public class AuthClientService : HttpClientBase ,IAuthClientService
{
    public AuthClientService(
        HttpClient httpClient,
        ErrorHandlingService errorHandler)
        : base(httpClient, errorHandler)
    {
    }

    public async Task<LoginResponse> Login(LoginRequestDto loginRequest)
    {
        return await PostAsync<LoginResponse>("api/auth/login", loginRequest);
    }

    public async Task Test()
    {
        await _httpClient.GetFromJsonAsync<List<object>>("api/auth/whoami");
    }
    
    public async Task<LoginResponse> Register(RegisterRequestDto registerRequest)
    {
        return await PostAsync<LoginResponse>("api/auth/register", registerRequest);
    }

    public async Task Logout()
    {
        await PostAsync("api/auth/logout");
    }

    public async Task<bool> IsUserAuthenticated()
    {
        var response = await _httpClient.GetAsync("api/auth/ping");
        return response.IsSuccessStatusCode;
    }
}
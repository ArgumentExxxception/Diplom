using System.Security.Claims;
using App.Models;
using Blazored.LocalStorage;
using Core.DTOs;
using Microsoft.AspNetCore.Components.Authorization;

namespace App.Services
{
    /// <summary>
    /// Провайдер состояния аутентификации для Blazor,
    /// использующий JWT токены, хранящиеся в локальном хранилище
    /// </summary>
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticationState _anonymous;

        public CustomAuthStateProvider(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Запрашиваем текущего пользователя
                var response = await _httpClient.GetFromJsonAsync<ApiResponse<UserDto>>("api/auth/currentuser");
            
                if (response?.Success != true || response.Data == null)
                    return _anonymous;

                var user = response.Data;
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email)
                };
            
                foreach (var role in user.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            
                return new AuthenticationState(
                    new ClaimsPrincipal(
                        new ClaimsIdentity(claims, "jwt")));
            }
            catch
            {
                return _anonymous;
            }
        }

        public void NotifyUserAuthentication()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public void NotifyUserLogout()
        {
            NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
        }
    }
}
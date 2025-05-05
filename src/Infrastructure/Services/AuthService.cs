using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core;
using Core.DTOs;
using Core.Entities;
using Core.Results;
using Domain.Entities;
using Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

public class AuthService: IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    
    public AuthService(IUnitOfWork unitOfWork, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequestDto loginRequest)
    {
        var response = new LoginResponse { Successful = false };

        var user = await _unitOfWork.Users.GetByEmailAsync(loginRequest.Username);
        if (user == null)
        {
            response.Error = "Пользователь не найден";
            return response;
        }

        if (!SecurityService.VerifyPassword(loginRequest.Password, user.Salt, user.PasswordHash))
        {
            response.Error = "Неверный пароль";
            return response;
        }
        
        user.LastLogin = DateTime.UtcNow;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.CommitAsync();
        
        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();
        
        var userRefreshToken = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            Created = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(7),
            User = user,
            CreatedByIp = string.Empty,
            ReasonRevoked = string.Empty,
            ReplacedByToken = string.Empty,
            RevokedByIp = string.Empty,
            IsRevoked = false,
        };
        
        await _unitOfWork.RefreshTokens.Add(userRefreshToken);
        await _unitOfWork.CommitAsync();

        response.Successful = true;
        response.Token = token;
        response.RefreshToken = refreshToken;
        response.User = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Roles = user.Roles
        };

        return response;
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequestDto registerRequest)
    {
        var response = new LoginResponse { Successful = false };

        // Validate request
        if (registerRequest.Password != registerRequest.ConfirmPassword)
        {
            response.Error = "Пароли не совпадают";
            return response;
        }

        var existingUserByName = await _unitOfWork.Users.GetByUsernameAsync(registerRequest.Username);
        if (existingUserByName != null)
        {
            response.Error = "Имя пользователя уже занято";
            return response;
        }

        var existingUserByEmail = await _unitOfWork.Users.GetByEmailAsync(registerRequest.Email);
        if (existingUserByEmail != null)
        {
            response.Error = "Email уже зарегистрирован";
            return response;
        }
        
        var salt = SecurityService.GenerateSalt();
        var passwordHash = SecurityService.HashPassword(registerRequest.Password, salt);
        
        var user = new User
        {
            Username = registerRequest.Username,
            Email = registerRequest.Email,
            PasswordHash = passwordHash,
            Salt = salt,
            Roles = new List<string> { "User" },
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.Add(user);
        await _unitOfWork.CommitAsync();
        
        var loginRequest = new LoginRequestDto
        {
            Username = registerRequest.Username,
            Password = registerRequest.Password
        };

        return await LoginAsync(loginRequest);
    }
    
    public async Task<LoginResponse> RefreshTokenAsync(string token, string refreshToken)
    {
        var response = new LoginResponse { Successful = false };

        // Проверяем, действителен ли текущий токен (кроме срока действия)
        var principal = GetPrincipalFromExpiredToken(token);
        if (principal == null)
        {
            response.Error = "Недействительный токен доступа";
            return response;
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            response.Error = "Токен не содержит идентификатор пользователя";
            return response;
        }

        // Находим запись refresh token в базе данных
        var storedRefreshToken = await _unitOfWork.RefreshTokens.GetByToken(refreshToken);
        if (storedRefreshToken == null)
        {
            response.Error = "Недействительный refresh token";
            return response;
        }

        if (storedRefreshToken.Expires < DateTime.UtcNow)
        {
            // Удаляем просроченный токен
            await _unitOfWork.RefreshTokens.Delete(storedRefreshToken.Id);
            await _unitOfWork.CommitAsync();
            
            response.Error = "Refresh token истек";
            return response;
        }

        // Проверяем, соответствует ли токен пользователю
        if (storedRefreshToken.UserId.ToString() != userId)
        {
            response.Error = "Refresh token не соответствует пользователю";
            return response;
        }

        // Получаем пользователя
        var user = await _unitOfWork.Users.GetByIdAsync(int.Parse(userId));
        if (user == null)
        {
            response.Error = "Пользователь не найден";
            return response;
        }

        // Генерируем новые токены
        var newToken = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();

        // Обновляем запись refresh token в базе данных
        storedRefreshToken.Token = newRefreshToken;
        storedRefreshToken.Created = DateTime.UtcNow;
        storedRefreshToken.Expires = DateTime.UtcNow.AddDays(7);
        
        await _unitOfWork.RefreshTokens.Update(storedRefreshToken);
        await _unitOfWork.CommitAsync();

        // Формируем успешный ответ
        response.Successful = true;
        response.Token = newToken;
        response.RefreshToken = newRefreshToken;
        response.User = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Roles = user.Roles
        };

        return response;
    }
    
    public async Task<bool> LogoutAsync(string token)
    {
        try
        {
            var principal = GetPrincipalFromToken(token);
            if (principal == null)
            {
                return false;
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            await _unitOfWork.RefreshTokens.RemoveAllForUser(int.Parse(userId));
            await _unitOfWork.CommitAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserDto> GetUserFromTokenAsync(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
    
        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = int.Parse(jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value);
        
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("Пользователь не найден");
            
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles
            };
        }
        catch
        {
            throw new Exception("Недействительный токен");
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
            
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
        };
        
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(double.Parse(_configuration["Jwt:ExpireHours"]));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

    // Метод для получения ClaimsPrincipal из токена, когда токен истек
    private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"],
            ValidateLifetime = false // Отключаем проверку срока действия
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            
            if (securityToken is not JwtSecurityToken jwtSecurityToken || 
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    // Метод для получения ClaimsPrincipal из токена с проверкой срока действия
    private ClaimsPrincipal GetPrincipalFromToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"],
            ValidateLifetime = true
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    // Метод для получения даты истечения срока действия токена
    private DateTime GetTokenExpiryDate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var expiry = jwtToken.ValidTo;
        return expiry;
    }
}
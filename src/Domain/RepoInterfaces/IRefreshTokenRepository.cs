using Domain.Entities;

namespace Domain.RepoInterfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>
    /// Получить refresh токен по его значению
    /// </summary>
    Task<RefreshToken> GetByToken(string token);

    /// <summary>
    /// Получить все токены пользователя
    /// </summary>
    Task<IEnumerable<RefreshToken>> GetAllForUser(int userId);

    /// <summary>
    /// Удалить все токены пользователя
    /// </summary>
    Task RemoveAllForUser(int userId);
}
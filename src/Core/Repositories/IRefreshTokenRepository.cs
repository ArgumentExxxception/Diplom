using Core.Entities;

namespace Core.Repositories;

public interface IRefreshTokenRepository
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
    /// Добавить новый refresh токен
    /// </summary>
    Task Add(RefreshToken refreshToken);

    /// <summary>
    /// Обновить существующий refresh токен
    /// </summary>
    Task Update(RefreshToken refreshToken);

    /// <summary>
    /// Удалить refresh токен по его ID
    /// </summary>
    Task Remove(int id);

    /// <summary>
    /// Удалить все токены пользователя
    /// </summary>
    Task RemoveAllForUser(int userId);
}
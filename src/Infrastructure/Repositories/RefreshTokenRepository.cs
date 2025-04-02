using Domain.Entities;
using Domain.RepoInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class RefreshTokenRepository: IRefreshTokenRepository
{
    private readonly Context _context;

    public RefreshTokenRepository(Context context)
    {
        _context = context;
    }

    /// <summary>
    /// Получить refresh токен по его значению
    /// </summary>
    public async Task<RefreshToken> GetByToken(string token)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    /// <summary>
    /// Получить все токены пользователя
    /// </summary>
    public async Task<IEnumerable<RefreshToken>> GetAllForUser(int userId)
    {
        return await _context.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<RefreshToken>> GetAllAsync()
    {
        return await _context.RefreshTokens.ToListAsync();
    }

    public async Task<RefreshToken> GetByIdAsync(int id)
    {
        return await _context.RefreshTokens.FindAsync(id);
    }

    public async Task<bool> Delete(int id)
    {
        var token = await _context.RefreshTokens.FindAsync(id);
        if (token != null)
        {
            _context.RefreshTokens.Remove(token);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Добавить новый refresh токен
    /// </summary>
    public async Task<bool> Add(RefreshToken refreshToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken);
        return true;
    }

    /// <summary>
    /// Обновить существующий refresh токен
    /// </summary>
    public async Task<bool> Update(RefreshToken refreshToken)
    {
         _context.RefreshTokens.Update(refreshToken);
        return true;
    }

    /// <summary>
    /// Удалить все токены пользователя
    /// </summary>
    public async Task RemoveAllForUser(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(tokens);
    }
}
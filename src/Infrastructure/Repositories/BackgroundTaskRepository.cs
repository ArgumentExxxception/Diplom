using System.Text.Json;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.RepoInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class BackgroundTaskRepository : IBackgroundTaskRepository
{
    private readonly Context _context;
    private readonly IMapper _mapper;

    public BackgroundTaskRepository(Context context, IMapper mapper = default)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<BackgroundTaskEntity>> GetActiveTasksAsync()
    {
        return await _context.BackgroundTasks
            .Where(t => t.Status == BackgroundTaskStatus.Pending || t.Status == BackgroundTaskStatus.Running)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<BackgroundTaskEntity> GetByIdAsync(Guid id)
    {
        return await _context.BackgroundTasks.FindAsync(id);
    }

    public async Task<List<BackgroundTaskEntity>> GetTasksByUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return await _context.BackgroundTasks
                .OrderByDescending(t => t.CreatedAt)
                .Take(100) // Ограничиваем количество для оптимизации
                .ToListAsync();

        return await _context.BackgroundTasks
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<BackgroundTaskEntity>> GetTasksByStatusAsync(BackgroundTaskStatus status)
    {
        return await _context.BackgroundTasks
            .Where(t => t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(Guid id, BackgroundTaskStatus status, int progress = 0,
        string errorMessage = null)
    {
        var task = await _context.BackgroundTasks.FindAsync(id);
        if (task != null)
        {
            task.Status = status;

            if (progress > 0)
                task.Progress = progress;

            if (!string.IsNullOrEmpty(errorMessage))
                task.ErrorMessage = errorMessage;

            if (status == BackgroundTaskStatus.Running && !task.StartedAt.HasValue)
                task.StartedAt = DateTime.UtcNow;

            if (status is BackgroundTaskStatus.Completed or BackgroundTaskStatus.Failed
                    or BackgroundTaskStatus.Cancelled && !task.CompletedAt.HasValue)
                task.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    public async Task CompleteTaskAsync(Guid id, object result = null)
    {
        var task = await _context.BackgroundTasks.FindAsync(id);
        if (task != null)
        {
            task.Status = BackgroundTaskStatus.Completed;
            task.Progress = 100;
            task.CompletedAt = DateTime.UtcNow;

            if (result != null)
                task.ResultDataJson = JsonSerializer.Serialize(result);

            await _context.SaveChangesAsync();
        }
    }

    public async Task FailTaskAsync(Guid id, string errorMessage)
    {
        var task = await _context.BackgroundTasks.FindAsync(id);
        if (task != null)
        {
            task.Status = BackgroundTaskStatus.Failed;
            task.ErrorMessage = errorMessage;
            task.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<BackgroundTaskEntity>> GetRecentlyCompletedTasksAsync(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        return await _context.BackgroundTasks
            .Where(t => (t.Status == BackgroundTaskStatus.Completed ||
                         t.Status == BackgroundTaskStatus.Failed ||
                         t.Status == BackgroundTaskStatus.Cancelled) &&
                        t.CompletedAt.HasValue &&
                        t.CompletedAt > cutoff)
            .OrderByDescending(t => t.CompletedAt)
            .ToListAsync();
    }

    public async Task CleanupOldTasksAsync(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        var oldTasks = await _context.BackgroundTasks
            .Where(t => (t.Status == BackgroundTaskStatus.Completed ||
                         t.Status == BackgroundTaskStatus.Failed ||
                         t.Status == BackgroundTaskStatus.Cancelled) &&
                        t.CompletedAt.HasValue &&
                        t.CompletedAt < cutoff)
            .ToListAsync();

        if (oldTasks.Any())
        {
            _context.BackgroundTasks.RemoveRange(oldTasks);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RequestCancellationAsync(Guid id)
    {
        var task = await _context.BackgroundTasks.FindAsync(id);
        if (task != null)
        {
            task.CancellationRequested = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsCancellationRequestedAsync(Guid id)
    {
        var task = await _context.BackgroundTasks.FindAsync(id);
        return task?.CancellationRequested ?? false;
    }

    // Реализация интерфейса IRepository
    public async Task<IEnumerable<BackgroundTaskEntity>> GetAllAsync()
    {
        return await _context.BackgroundTasks.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public Task<BackgroundTaskEntity> GetByIdAsync(int id)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> Add(BackgroundTaskEntity entity)
    {
        await _context.BackgroundTasks.AddAsync(entity);
        return true;
    }

    public async Task<bool> Update(BackgroundTaskEntity entity)
    {
        _context.BackgroundTasks.Update(entity);
        return true;
    }

    public async Task<bool> Delete(int id)
    {
        // Это метод из базового интерфейса IRepository, который использует int.
        // Так как наша сущность использует Guid, создадим отдельный метод для удаления
        throw new NotImplementedException("Use DeleteByGuid instead");
    }

    public async Task<bool> DeleteByGuid(Guid id)
    {
        var task = await _context.BackgroundTasks.FindAsync(id);
        if (task != null)
        {
            _context.BackgroundTasks.Remove(task);
            return true;
        }

        return false;
    }
}
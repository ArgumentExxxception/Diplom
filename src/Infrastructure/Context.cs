using System.Text.Json;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class Context: DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public Context(DbContextOptions<Context> options): base(options)
    {
        
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Salt).IsRequired();
                
            // Store roles as JSON
            entity.Property(e => e.Roles)
                .HasConversion(
                    v => JsonSerializer.Serialize<List<string>>(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<List<string>>(v, new JsonSerializerOptions()) ?? new List<string>()
                );
        }).HasDefaultSchema("appschema");
        
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Created).IsRequired();
            entity.Property(e => e.Expires).IsRequired();
            entity.Property(e => e.CreatedByIp).HasMaxLength(50);
            entity.Property(e => e.RevokedByIp).HasMaxLength(50);
            entity.Property(e => e.ReplacedByToken).HasMaxLength(256);
            entity.Property(e => e.ReasonRevoked).HasMaxLength(256);

            // Связь с пользователем
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }).HasDefaultSchema("appschema");
    }
}
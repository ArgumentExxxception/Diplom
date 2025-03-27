using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

public class RefreshToken
    {
        /// <summary>
        /// Уникальный идентификатор токена
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Идентификатор пользователя, которому принадлежит токен
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Навигационное свойство для связи с пользователем
        /// </summary>
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        /// <summary>
        /// Сам токен (случайная строка)
        /// </summary>
        [Required]
        [MaxLength(256)]
        public string Token { get; set; }

        /// <summary>
        /// Дата и время создания токена
        /// </summary>
        [Required]
        public DateTime Created { get; set; }

        /// <summary>
        /// Дата и время истечения срока действия токена
        /// </summary>
        [Required]
        public DateTime Expires { get; set; }

        /// <summary>
        /// Флаг, указывающий, был ли токен отозван
        /// </summary>
        public bool IsRevoked { get; set; }

        /// <summary>
        /// IP-адрес, с которого был создан токен
        /// </summary>
        [MaxLength(50)]
        public string CreatedByIp { get; set; }

        /// <summary>
        /// Дата отзыва токена
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// IP-адрес, с которого был отозван токен
        /// </summary>
        [MaxLength(50)]
        public string RevokedByIp { get; set; }

        /// <summary>
        /// Токен замены (если текущий токен был заменен новым)
        /// </summary>
        [MaxLength(256)]
        public string ReplacedByToken { get; set; }

        /// <summary>
        /// Причина отзыва токена
        /// </summary>
        [MaxLength(256)]
        public string ReasonRevoked { get; set; }

        /// <summary>
        /// Флаг, указывающий, активен ли токен
        /// </summary>
        public bool IsActive => !IsRevoked && DateTime.UtcNow < Expires;
    }
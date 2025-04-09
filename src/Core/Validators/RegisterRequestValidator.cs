using Core.DTOs;
using FluentValidation;

namespace Core.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Имя пользователя обязательно")
            .Length(3, 50).WithMessage("Длина имени пользователя должна быть от 3 до 50 символов")
            .Matches(@"^[a-zA-Z0-9]+$").WithMessage("Имя пользователя может содержать только буквы и цифры");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен")
            .EmailAddress().WithMessage("Некорректный формат email");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен")
            .MinimumLength(8).WithMessage("Пароль должен содержать не менее 8 символов")
            .Matches(@"[A-Za-z]").WithMessage("Пароль должен содержать хотя бы одну букву")
            .Matches(@"[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Подтверждение пароля обязательно")
            .Equal(x => x.Password).WithMessage("Пароли не совпадают");
    }
}
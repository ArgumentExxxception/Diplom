using Core.Models;
using FluentValidation;

namespace Core.Validators;

public class LoginModelValidator : AbstractValidator<LoginModel>
{
    public LoginModelValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен")
            .EmailAddress().WithMessage("Некорректный формат email")
            .MaximumLength(100).WithMessage("Максимальная длина 100 символов");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен")
            .MinimumLength(8).WithMessage("Минимум 8 символов")
            .MaximumLength(50).WithMessage("Максимум 50 символов")
            .Matches("[A-Z]").WithMessage("Должна быть хотя бы одна заглавная буква")
            .Matches("[a-z]").WithMessage("Должна быть хотя бы одна строчная буква")
            .Matches("[0-9]").WithMessage("Должна быть хотя бы одна цифра");
    }
}
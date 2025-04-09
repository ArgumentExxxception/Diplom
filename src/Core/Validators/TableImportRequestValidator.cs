using Core.Models;
using FluentValidation;

namespace Core.Validators;

public class TableImportRequestValidator : AbstractValidator<TableImportRequestModel>
{
    public TableImportRequestValidator()
    {
        RuleFor(x => x.TableName)
            .NotEmpty().WithMessage("Название таблицы обязательно");

        RuleFor(x => x.Columns)
            .NotEmpty().WithMessage("Список колонок не может быть пустым");

        RuleFor(x => x.UserEmail)
            .NotEmpty().WithMessage("Email пользователя обязателен")
            .EmailAddress().WithMessage("Некорректный формат email");

        When(x => x.IsNewTable, () =>
        {
            RuleFor(x => x.Columns)
                .Must(columns => columns.Any(c => c.IsPrimaryKey))
                .WithMessage("Для новой таблицы должен быть указан хотя бы один первичный ключ");
        });
    }
}
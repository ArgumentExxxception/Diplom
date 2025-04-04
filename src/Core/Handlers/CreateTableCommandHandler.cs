using Core.Commands;
using MediatR;

namespace Core.Handlers;

public class CreateTableCommandHandler: IRequestHandler<CreateTableCommand, string>
{
    private readonly IDatabaseService _databaseService;

    public CreateTableCommandHandler(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<string> Handle(CreateTableCommand request, CancellationToken cancellationToken)
    {
        if (request.tableModel == null || string.IsNullOrEmpty(request.tableModel.TableName))
        {
            throw new ArgumentException("Некорректные данные для создания таблицы.");
        }
        
        await _databaseService.CreateTableAsync(request.tableModel);
        return "Таблица успешно создана.";
    }
}
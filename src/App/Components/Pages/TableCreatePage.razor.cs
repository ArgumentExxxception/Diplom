using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Pages;

public partial class TableCreatePage : ComponentBase
{
    private string tableName = string.Empty;
    private string newColumnName = string.Empty;
    private Types newColumnType = Types.Text; // По умолчанию текст
    private List<ColumnInfo> columns = new();

    private void AddColumn()
    {
        if (string.IsNullOrWhiteSpace(newColumnName))
        {
            Snackbar.Add("Введите название колонки.", Severity.Warning);
            return;
        }

        columns.Add(new ColumnInfo
        {
            Name = newColumnName,
            Type = newColumnType
        });

        // Очищаем поля после добавления
        newColumnName = string.Empty;
        newColumnType = Types.Text;
    }

    private async Task CreateTable()
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            Snackbar.Add("Введите название таблицы.", Severity.Warning);
            return;
        }

        if (columns.Count == 0)
        {
            Snackbar.Add("Добавьте хотя бы одну колонку.", Severity.Warning);
            return;
        }

        // Отправляем данные на сервер
        var tableData = new TableCreationRequest
        {
            TableName = tableName,
            Columns = columns
        };

        try
        {
            // await WebRequestMethods.Http.PostAsJsonAsync("/api/table/create", tableData);
            Snackbar.Add("Таблица успешно создана!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Ошибка: {ex.Message}", Severity.Error);
        }
    }

    public class ColumnInfo
    {
        public string Name { get; set; }
        public Types Type { get; set; }
    }

    public class TableCreationRequest
    {
        public string TableName { get; set; }
        public List<ColumnInfo> Columns { get; set; }
    }

    public enum Types
    {
        [Display(Name = "Текст")]
        Text = 0,
        [Display(Name = "Число")]
        Number = 1,
        [Display(Name = "Дата")]
        Date = 2,
        [Display(Name = "Логическое выражение")]
        Boolean = 3,
        [Display(Name = "Короткий текст")]
        Varchar = 4,
    }
}
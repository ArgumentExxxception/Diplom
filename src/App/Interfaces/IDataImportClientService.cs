using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;

namespace App.Interfaces;

public interface IDataImportClientService
{
    Task<ImportResult> ImportData(IBrowserFile file, TableImportRequestModel importRequest,CancellationToken cancellationToken = default);
    Task UpdateDuplicate(string tableName, List<Dictionary<string, object>> duplicates);
}
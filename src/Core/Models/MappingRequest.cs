namespace Core.Models;

public class MappingRequest
{
    public Dictionary<string, string> MappedColumns { get; set; }
    public List<Dictionary<string, string>> FileData { get; set; }
}
namespace SqlTelemetryAssistant.Api.Models;

public class AiSqlRequest
{
    public string Question { get; set; } = "";
}

public class AiSqlResponse
{
    public string Question { get; set; } = "";
    public string GeneratedSql { get; set; } = "";
    public string Explanation { get; set; } = "";
    public List<Dictionary<string, object?>> ResultPreview { get; set; } = new();
}
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SqlTelemetryAssistant.Api.Models;

namespace SqlTelemetryAssistant.Api.Services;

public class AiSqlService
{
    private readonly OpenAiRecommendationProvider _ai;
    private readonly string _connectionString;

    public AiSqlService(OpenAiRecommendationProvider ai, IConfiguration cfg)
    {
        _ai = ai;
        _connectionString = cfg.GetSection("SqlServer")["ConnectionString"]
                            ?? throw new InvalidOperationException("Missing SqlServer:ConnectionString");
    }

    public async Task<AiSqlResponse> HandleQuestionAsync(string question)
    {
        var prompt = $@"
Jesteś ekspertem SQL Server.

Użytkownik zadaje pytanie diagnostyczne dotyczące instancji SQL Server:

""{question}""

Twoje zadanie:
1. Zaproponuj jedno zapytanie T-SQL, które pomaga zdiagnozować temat pytania.
2. Używaj WYŁĄCZNIE widoków katalogowych i DMV:
   - sys.dm_*
   - sys.objects, sys.tables, sys.indexes, sys.databases
   - sys.query_store_query_text, sys.query_store_query,
     sys.query_store_plan, sys.query_store_runtime_stats,
     sys.query_store_runtime_stats_interval
3. Zapytanie musi być:
   - WYŁĄCZNIE SELECT,
   - bez modyfikacji danych,
   - bez DDL (CREATE/ALTER/DROP/TRUNCATE),
   - bez EXEC, bez procedur systemowych (sp_*, xp_*).
4. Zakładaj SQL Server 2022 Developer Edition:
   - NIE używaj kolumn, które występują tylko w Azure SQL lub nowszych wersjach:
     avg_cpu_time, execution_type_desc, avg_query_max_used_memory,
     avg_query_max_used_grant, wait_stats_count, max_dop.
5. Odpowiedz WYŁĄCZNIE prawidłowym JSON-em, bez markdown, bez ```json, bez komentarzy,
   bez dodatkowego tekstu przed ani po JSON.

JSON MUSI mieć dokładnie taki format:

{{
  ""sql"": ""TU JEDEN SELECT"",
  ""explanation"": ""KRÓTKIE WYJAŚNIENIE PO POLSKU""
}}";

        var raw = await _ai.GetRecommendationAsync(prompt);

        string sql;
        string explanation;

        try
        {
            // 🔧 „Oczyszczanie” – usuwamy ewentualne backticki / markdown / entery
            var cleaned = raw
                .Trim()
                .Trim('`')              // jakby model dał ``` na początku/końcu
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            sql = root.GetProperty("sql").GetString() ?? "";
            explanation = root.GetProperty("explanation").GetString() ?? "";
        }
        catch
        {
            // Fallback: potraktuj odpowiedź jako czysty T-SQL
            sql = raw.Trim();
            explanation = "Model nie zwrócił poprawnego JSON – potraktowano odpowiedź jako T-SQL.";
        }

        var response = new AiSqlResponse
        {
            Question = question,
            GeneratedSql = sql,
            Explanation = explanation
        };

        // Walidacja bezpieczeństwa – czy to w ogóle wygląda na bezpieczny SELECT?
        if (!IsSafeDiagnosticQuery(sql))
        {
            response.Explanation += " Zapytanie nie przeszło walidacji bezpieczeństwa i nie zostało wykonane.";
            return response;
        }

        try
        {
            response.ResultPreview = await ExecutePreviewAsync(sql);
        }
        catch (SqlException ex)
        {
            // Zamiast 500 – błąd doklejony do wyjaśnienia
            response.Explanation += $" Podczas wykonywania zapytania wystąpił błąd SQL: {ex.Message}";
        }

        return response;
    }

    private static bool IsSafeDiagnosticQuery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        var upper = sql.Trim().ToUpperInvariant();

        // Musi zaczynać się od SELECT (po trimie)
        if (!upper.StartsWith("SELECT", StringComparison.Ordinal))
            return false;

        // Minimalny firewall na rzeczy niebezpieczne
        string[] banned =
        {
            " INSERT ", " UPDATE ", " DELETE ", " MERGE ",
            " ALTER ", " DROP ", " TRUNCATE ", " CREATE ",
            " EXEC ", "EXEC(", "sp_", "xp_"
        };

        foreach (var bad in banned)
        {
            if (upper.Contains(bad, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private async Task<List<Dictionary<string, object?>>> ExecutePreviewAsync(string sql)
    {
        var result = new List<Dictionary<string, object?>>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandType = CommandType.Text
        };

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult);

        int rowCount = 0;
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                row[name] = value;
            }

            result.Add(row);
            rowCount++;
            if (rowCount >= 50)
                break; // tylko podgląd
        }

        return result;
    }
}

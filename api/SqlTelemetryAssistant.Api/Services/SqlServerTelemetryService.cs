using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace SqlTelemetryAssistant.Api.Services;

public class SqlServerSnapshot
{
    public string ServerName { get; set; } = "";
    public string ProductVersion { get; set; } = "";
    public int DatabasesCount { get; set; }
    public int UserSessions { get; set; }
    public DateTime CapturedAtUtc { get; set; }

    public IReadOnlyList<string> TopWaits { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TopIoFiles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TopMemoryClerks { get; set; } = Array.Empty<string>();
}
public class SqlServerTelemetryService
{
    private readonly string _connectionString;

    public SqlServerTelemetryService(IConfiguration configuration)
    {
        _connectionString = configuration.GetSection("SqlServer")["ConnectionString"]
                            ?? throw new InvalidOperationException("Missing SqlServer:ConnectionString");
    }

    public async Task<SqlServerSnapshot> GetBasicSnapshotAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
SET NOCOUNT ON;

-- 1) Podstawowe info
SELECT
    @@SERVERNAME AS ServerName,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
    (SELECT COUNT(*) FROM sys.databases) AS DatabasesCount,
    (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) AS UserSessions;

-- 2) TOP waits
SELECT TOP (5)
    wait_type,
    wait_time_ms,
    signal_wait_time_ms,
    waiting_tasks_count
FROM sys.dm_os_wait_stats
WHERE wait_time_ms > 0
  AND wait_type NOT LIKE 'BROKER_%'
  AND wait_type NOT IN ('SLEEP_TASK', 'SLEEP_SYSTEMTASK', 'SQLTRACE_BUFFER_FLUSH')
ORDER BY wait_time_ms DESC;

-- 3) TOP I/O
SELECT TOP (5)
    DB_NAME(vfs.database_id) AS database_name,
    mf.name AS file_name,
    mf.type_desc,
    vfs.num_of_reads,
    vfs.num_of_writes,
    vfs.num_of_bytes_read,
    vfs.num_of_bytes_written
FROM sys.dm_io_virtual_file_stats(NULL, NULL) AS vfs
JOIN sys.master_files AS mf
  ON vfs.database_id = mf.database_id AND vfs.file_id = mf.file_id
ORDER BY (vfs.num_of_reads + vfs.num_of_writes) DESC;

-- 4) TOP memory clerks
SELECT TOP (5)
    type,
    pages_kb / 1024.0 AS memory_mb
FROM sys.dm_os_memory_clerks
WHERE pages_kb > 0
ORDER BY pages_kb DESC;
";

        await using var cmd = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text
        };

        await using var reader = await cmd.ExecuteReaderAsync();

        var snapshot = new SqlServerSnapshot
        {
            CapturedAtUtc = DateTime.UtcNow
        };

        // 1) podstawowe info
        if (await reader.ReadAsync())
        {
            snapshot.ServerName = reader.GetString(reader.GetOrdinal("ServerName"));
            snapshot.ProductVersion = reader.GetString(reader.GetOrdinal("ProductVersion"));
            snapshot.DatabasesCount = reader.GetInt32(reader.GetOrdinal("DatabasesCount"));
            snapshot.UserSessions = reader.GetInt32(reader.GetOrdinal("UserSessions"));
        }

        // 2) waits
        var waits = new List<string>();
        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                var waitType = reader.GetString(0);
                var waitMs = reader.GetInt64(1);
                var signalMs = reader.GetInt64(2);
                var tasks = reader.GetInt64(3);

                waits.Add(
                    $"{waitType}: wait={waitMs} ms, signal={signalMs} ms, tasks={tasks}"
                );
            }
        }
        snapshot.TopWaits = waits;

        // 3) I/O
        var ioFiles = new List<string>();
        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                var dbName = reader.GetString(0);
                var fileName = reader.GetString(1);
                var typeDesc = reader.GetString(2);
                var reads = reader.GetInt64(3);
                var writes = reader.GetInt64(4);

                ioFiles.Add(
                    $"{dbName}/{fileName} ({typeDesc}): reads={reads}, writes={writes}"
                );
            }
        }
        snapshot.TopIoFiles = ioFiles;

        // 4) memory clerks
        var memClerks = new List<string>();
        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                var type = reader.GetString(0);
                var mb = reader.GetDecimal(1);
                memClerks.Add($"{type}: {mb:F1} MB");
            }
        }
        snapshot.TopMemoryClerks = memClerks;

        return snapshot;
    }

    public string BuildPrompt(SqlServerSnapshot snapshot)
    {
        var waitsText = snapshot.TopWaits.Any()
            ? string.Join("\n", snapshot.TopWaits)
            : "Brak zebranych danych o wait stats.";

        var ioText = snapshot.TopIoFiles.Any()
            ? string.Join("\n", snapshot.TopIoFiles)
            : "Brak zebranych danych I/O.";

        var memText = snapshot.TopMemoryClerks.Any()
            ? string.Join("\n", snapshot.TopMemoryClerks)
            : "Brak danych o memory clerks.";

        return $@"
Masz dostęp do podstawowych informacji o instancji SQL Server:

- Nazwa serwera: {snapshot.ServerName}
- Wersja produktu: {snapshot.ProductVersion}
- Liczba baz danych: {snapshot.DatabasesCount}
- Liczba aktywnych sesji użytkowników: {snapshot.UserSessions}
- Czas pobrania danych (UTC): {snapshot.CapturedAtUtc:O}

Dodatkowo masz skrócone dane diagnostyczne:

TOP waits:
{waitsText}

TOP I/O (wg liczby operacji odczytu/zapisu):
{ioText}

TOP memory clerks:
{memText}

Na podstawie tych danych:
1. Oceń, czy środowisko wygląda na DEV/TEST/PROD.
2. Zaproponuj 2–3 kolejne kroki diagnostyczne dla DBA.
3. Zasugeruj ewentualne obszary do optymalizacji.
Odpowiedz po polsku, maksymalnie w 8–10 zdaniach.
";
    }
}

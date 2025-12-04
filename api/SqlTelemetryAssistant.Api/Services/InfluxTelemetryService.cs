using System.Globalization;
using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using SqlTelemetryAssistant.Api.Models;

namespace SqlTelemetryAssistant.Api.Services;

public class InfluxTelemetryService
{
    private readonly string _url;
    private readonly string _org;
    private readonly string _bucket;
    private readonly string _token;

    public InfluxTelemetryService(IConfiguration config)
    {
        _url = config["Influx:Url"]
               ?? throw new InvalidOperationException("Missing config: Influx:Url");
        _org = config["Influx:Org"]
               ?? throw new InvalidOperationException("Missing config: Influx:Org");
        _bucket = config["Influx:Bucket"]
                 ?? throw new InvalidOperationException("Missing config: Influx:Bucket");
        _token = config["Influx:Token"]
                 ?? throw new InvalidOperationException("Missing config: Influx:Token");
    }

    /// <summary>
    /// Zwraca top 10 waitów z ostatnich 5 minut z InfluxDB
    /// jako lista WaitStatPulse pod konsolę.
    /// </summary>
    public async Task<IEnumerable<WaitStatPulse>> GetWaitStatsPulseAsync()
    {
        // Klient Influxa – przyjmuje string, nie char[]
        using var client = new InfluxDBClient(_url, _token);
        var queryApi = client.GetQueryApi();

        // Dopasowane do tego, co masz w Data Explorer:
        //  bucket = sql_telemetry
        //  _measurement = sqlserver_waitstats
        //  _field = resource_wait_ms
        var flux = $@"
from(bucket: ""{_bucket}"")
  |> range(start: -5m)
  |> filter(fn: (r) => r._measurement == ""sqlserver_waitstats"")
  |> filter(fn: (r) => r._field == ""resource_wait_ms"")
  |> group(columns: [""wait_type""])
  |> sum()
  |> sort(columns: [""_value""], desc: true)
  |> limit(n: 10)
";

        var tables = await queryApi.QueryAsync(flux, _org);

        // Najpierw zbieramy surowe wartości: WaitType + TotalWaitMs
        var raw = new List<(string WaitType, double TotalWaitMs)>();

        foreach (var table in tables)
        {
            foreach (var record in table.Records)
            {
                var waitType = record.GetValueByKey("wait_type")?.ToString();
                if (string.IsNullOrWhiteSpace(waitType))
                    continue;

                var valueObj = record.GetValue();
                if (valueObj is null)
                    continue;

                var ms = Convert.ToDouble(valueObj, CultureInfo.InvariantCulture);

                raw.Add((waitType, ms));
            }
        }

        // policz sumę i procentowy udział
        var total = raw.Sum(w => w.TotalWaitMs);

        var result = new List<WaitStatPulse>();

        foreach (var item in raw)
        {
            var percentage = total > 0
                ? item.TotalWaitMs / total * 100.0
                : 0.0;

            // WaitStatPulse(string WaitType, double TotalWaitMs, double SignalWaitMs, double Percentage)
            var ws = new WaitStatPulse(
                item.WaitType,
                item.TotalWaitMs,
                0.0,           // SignalWaitMs – na razie brak danych, więc 0
                percentage);

            result.Add(ws);
        }

        return result;
    }
}

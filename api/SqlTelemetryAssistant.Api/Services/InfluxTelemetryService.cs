
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using SqlTelemetryAssistant.Api.Models;

namespace SqlTelemetryAssistant.Api.Services;

public class InfluxTelemetryService
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _org;
    private readonly string _bucket;
    private readonly string _token;

    public InfluxTelemetryService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _url = config["Influx:Url"] ?? "http://influxdb:8086";
        _org = config["Influx:Org"] ?? "Demo";
        _bucket = config["Influx:Bucket"] ?? "sql_telemetry";
        _token = config["Influx:Token"] ?? "dev-token";

        if (!string.IsNullOrWhiteSpace(_token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", _token);
        }
    }

    public async Task<IEnumerable<WaitStatPulse>> GetWaitStatsPulseAsync()
    {
        var flux = $@"
from(bucket: ""{_bucket}"")
  |> range(start: -10m)
  |> filter(fn: (r) => r[""_measurement""] == ""sqlserver_waitstats"")
  |> group(columns: [""wait_type""])
  |> sum(column: ""wait_time_ms"")
  |> rename(columns: {{ wait_time_ms: ""total_wait_ms"" }})
";

        var content = new StringContent(flux, Encoding.UTF8, "application/vnd.flux");
        var resp = await _httpClient.PostAsync($"{_url}/api/v2/query?org={Uri.EscapeDataString(_org)}", content);
        resp.EnsureSuccessStatusCode();

        var csv = await resp.Content.ReadAsStringAsync();

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataLines = lines.Where(l => !l.StartsWith("#")).ToList();

        var result = new List<WaitStatPulse>();

        if (dataLines.Count <= 1)
            return result;

        var header = dataLines[0].Split(',');
        var valueIndex = Array.FindIndex(header, h => h == "_value" || h.EndsWith("total_wait_ms", StringComparison.OrdinalIgnoreCase));
        var waitTypeIndex = Array.FindIndex(header, h => h == "wait_type");

        foreach (var line in dataLines.Skip(1))
        {
            var cols = line.Split(',');
            if (cols.Length <= Math.Max(valueIndex, waitTypeIndex) || valueIndex < 0 || waitTypeIndex < 0)
                continue;

            var waitType = cols[waitTypeIndex];
            if (!double.TryParse(cols[valueIndex], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var totalMs))
                totalMs = 0;

            result.Add(new WaitStatPulse(
                WaitType: waitType,
                TotalWaitMs: totalMs,
                SignalWaitMs: 0,
                Percentage: 0
            ));
        }

        if (!result.Any())
            return result;

        var sum = result.Sum(r => r.TotalWaitMs);
        if (sum > 0)
        {
            result = result
                .Select(r => r with { Percentage = r.TotalWaitMs * 100.0 / sum })
                .ToList();
        }

        return result
            .OrderByDescending(r => r.TotalWaitMs)
            .ToList();
    }
}

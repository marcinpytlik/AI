
namespace SqlTelemetryAssistant.Api.Models;

public record WaitStatPulse(
    string WaitType,
    double TotalWaitMs,
    double SignalWaitMs,
    double Percentage);

public class TelemetryResponse
{
    public DateTime GeneratedAtUtc { get; set; }
    public IEnumerable<WaitStatPulse> Telemetry { get; set; } = Array.Empty<WaitStatPulse>();
    public string Recommendation { get; set; } = string.Empty;
}

public class TelemetryWithKnowledgeResponse : TelemetryResponse
{
    public string[] KnowledgeFragments { get; set; } = Array.Empty<string>();
}

public static class PromptFactory
{
    public static string BuildPrompt(IEnumerable<WaitStatPulse> pulse)
    {
        var top = pulse
            .OrderByDescending(p => p.Percentage)
            .Take(5)
            .ToList();

        var lines = top.Select(p =>
            $"{p.WaitType}: {p.TotalWaitMs:F0} ms (signal: {p.SignalWaitMs:F0} ms), {p.Percentage:F1}% całkowitych waitów");

        var joined = string.Join("\n", lines);

        return $@"
Jesteś doświadczonym administratorem SQL Server (DBA).
Na podstawie poniższych statystyk waitów z ostatnich kilku minut:

{joined}

1. Zinterpretuj sytuację wydajnościową.
2. Podaj możliwe przyczyny.
3. Zaproponuj konkretne działania diagnostyczne i/lub naprawcze.

Odpowiedz po polsku, zwięźle, w formie punktów.";
    }

    public static string BuildPromptWithKnowledge(
        IEnumerable<WaitStatPulse> pulse,
        IEnumerable<string> knowledgeFragments)
    {
        var basePrompt = BuildPrompt(pulse);

        var kbText = string.Join("\n\n---\n\n", knowledgeFragments);

        return $@"
Masz dodatkowo dostęp do fragmentów dokumentacji wewnętrznej SQLManiaka:

{kbText}

Na tej podstawie oraz na podstawie poniższych statystyk wygeneruj rekomendacje,
preferując podejście, terminologię i dobre praktyki z dokumentacji.

{basePrompt}
";
    }
}

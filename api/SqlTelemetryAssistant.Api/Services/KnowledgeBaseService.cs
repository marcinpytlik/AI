
using System.Text;
using Microsoft.Extensions.Configuration;
using SqlTelemetryAssistant.Api.Models;

namespace SqlTelemetryAssistant.Api.Services;

public class KnowledgeBaseService
{
    private readonly string _rootPath;

    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public KnowledgeBaseService(IConfiguration config)
    {
        var root = config["Knowledge:RootPath"] ?? "../knowledge";
        _rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, root));

        if (Directory.Exists(_rootPath))
        {
            foreach (var file in Directory.GetFiles(_rootPath, "*.md", SearchOption.AllDirectories))
            {
                var key = Path.GetFileNameWithoutExtension(file);
                var content = File.ReadAllText(file, Encoding.UTF8);
                _cache[key] = content;
            }
        }
    }

    public IEnumerable<string> GetRelevantFragments(IEnumerable<WaitStatPulse> pulse)
    {
        if (_cache.Count == 0)
            return Array.Empty<string>();

        var waitTypes = pulse.Select(p => p.WaitType.ToUpperInvariant()).ToList();

        var fragments = new List<string>();

        if (_cache.TryGetValue("waitstats_basics", out var basics))
            fragments.Add(basics);

        if (waitTypes.Any(w => w.Contains("WRITELOG") || w.Contains("PAGEIOLATCH")))
        {
            if (_cache.TryGetValue("io_and_writelog", out var io))
                fragments.Add(io);
        }

        if (waitTypes.Any(w => w.Contains("CXPACKET") || w.Contains("CXCONSUMER")))
        {
            if (_cache.TryGetValue("cxpacket_parallelism", out var cx))
                fragments.Add(cx);
        }

        return fragments;
    }
}

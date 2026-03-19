using System.Text.RegularExpressions;

namespace MantelProgrammingTest.Services;

public partial class LogFileService : ILogFileService
{
    // Combined Log Format:
    // IP identity user [timestamp] "request" status size "referer" "user-agent"
    [GeneratedRegex(@"^\S+ \S+ \S+ \[.+\] ""[^""]*"" \d{3} (\d+|-) ""[^""]*"" ""[^""]*""$")]
    private static partial Regex CombinedLogFormatRegex();

    // Extracts the IP address (group 1) and the URL from the request field (group 2)
    // Example line: 177.71.128.21 - - [10/Jul/2018:22:21:28 +0200] "GET /intranet-analytics/ HTTP/1.1" 200 3574 "-" "Mozilla/5.0"
    //   Group 1 = 177.71.128.21
    //   Group 2 = /intranet-analytics/
    [GeneratedRegex(@"^(\S+) \S+ \S+ \[.+\] ""\S+\s+(\S+)\s+[^""]*"" \d{3} (\d+|-) ""[^""]*"" ""[^""]*""$")]
    private static partial Regex LogEntryParserRegex();

    public LogValidationResult Validate(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            return LogValidationResult.Fail("The file is empty.");

        var invalidLines = new List<(int LineNumber, string Line)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!CombinedLogFormatRegex().IsMatch(line))
                invalidLines.Add((i + 1, line));
        }

        return invalidLines.Count > 0
            ? LogValidationResult.Fail(invalidLines)
            : LogValidationResult.Success(lines.Length);
    }

    public LogInterrogationResult InterrogateLogFile(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var ipCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var urlCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = LogEntryParserRegex().Match(line);
            if (!match.Success)
                continue;

            var ip = match.Groups[1].Value;
            var url = match.Groups[2].Value;

            ipCounts[ip] = ipCounts.TryGetValue(ip, out var ipCount) ? ipCount + 1 : 1;
            urlCounts[url] = urlCounts.TryGetValue(url, out var urlCount) ? urlCount + 1 : 1;
        }

        var uniqueIpCount = ipCounts.Count;

        var topUrls = ipCounts.Count == 0
            ? []
            : urlCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .Select(kvp => new UrlVisitCount(kvp.Key, kvp.Value))
                .ToList();

        var topIps = ipCounts.Count == 0
            ? []
            : ipCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .Select(kvp => new IpActivityCount(kvp.Key, kvp.Value))
                .ToList();

        return new LogInterrogationResult
        {
            UniqueIpAddressCount = uniqueIpCount,
            TopVisitedUrls = topUrls,
            TopActiveIpAddresses = topIps
        };
    }
}

public class LogValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public int LineCount { get; init; }
    public List<(int LineNumber, string Line)> InvalidLines { get; init; } = [];

    public static LogValidationResult Success(int lineCount) =>
        new() { IsValid = true, LineCount = lineCount };

    public static LogValidationResult Fail(string message) =>
        new() { IsValid = false, ErrorMessage = message };

    public static LogValidationResult Fail(List<(int LineNumber, string Line)> invalidLines) =>
        new() { IsValid = false, InvalidLines = invalidLines };
}

public class LogInterrogationResult
{
    public int UniqueIpAddressCount { get; init; }
    public List<UrlVisitCount> TopVisitedUrls { get; init; } = [];
    public List<IpActivityCount> TopActiveIpAddresses { get; init; } = [];
}

public record UrlVisitCount(string Url, int VisitCount);

public record IpActivityCount(string IpAddress, int RequestCount);
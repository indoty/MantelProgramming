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

    /// <summary>
    /// Validates the content of a log file against the Combined Log Format. 
    /// It checks each line for conformity and collects any invalid lines along with their line numbers. 
    /// If the file is empty, it returns a failure result indicating that the file is empty. 
    /// If there are invalid lines, it returns a failure result with the list of invalid lines; 
    /// otherwise, it returns a success result with the total line count.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Interrogates the log file content to extract insights such as the count of unique IP addresses,
    /// the top visited URLs, and the top active IP addresses.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
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

/// <summary>
/// LogValidationResult represents the outcome of validating a log file's content. It indicates whether the validation was successful,
/// </summary>
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

/// <summary>
/// LogInterrogationResult represents the insights extracted from interrogating a log file's content. It includes the count of unique IP addresses,
/// the top visited URLs, and the top active IP addresses.
/// </summary>
public class LogInterrogationResult
{
    public int UniqueIpAddressCount { get; init; }
    public List<UrlVisitCount> TopVisitedUrls { get; init; } = [];
    public List<IpActivityCount> TopActiveIpAddresses { get; init; } = [];
}

/// <summary>
/// UrlVisitCount represents the count of visits to a specific URL. 
/// It contains the URL and the number of times it was visited.
/// </summary>
/// <param name="Url"></param>
/// <param name="VisitCount"></param>
public record UrlVisitCount(string Url, int VisitCount);

/// <summary>
/// IpActivityCount represents the count of requests made by a specific IP address. 
/// It contains the IP address and the number of requests associated with it.
/// </summary>
/// <param name="IpAddress"></param>
/// <param name="RequestCount"></param>
public record IpActivityCount(string IpAddress, int RequestCount);
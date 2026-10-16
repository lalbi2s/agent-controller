namespace AgentController.Services;

public class FileChunkingService
{
    private const int Overlap = 500;

    public bool NeedsChunking(string content, int maxChars) => content.Length > maxChars;

    public List<string> Split(string content, int chunkSize)
    {
        if (chunkSize <= Overlap)
            throw new ArgumentException($"The chunk size must be larger than the overlap ({Overlap})", nameof(chunkSize));

        var chunks = new List<string>();
        int pos = 0;

        while (pos < content.Length)
        {
            int end = Math.Min(pos + chunkSize, content.Length);
            chunks.Add(content[pos..end]);
            pos = end - Overlap;
            if (pos < 0) pos = 0;
            if (end >= content.Length) break;
        }

        return chunks;
    }

    public string MergeResults(List<string> results, string outputType)
    {
        if (results.Count <= 1)
            return results.FirstOrDefault() ?? string.Empty;

        return outputType.ToLower() switch
        {
            "json"     => MergeJsonArrays(results),
            "html"     => MergeHtml(results),
            "csv"      => MergeCsv(results),
            "markdown" => MergeMarkdown(results),
            _          => MergeText(results)
        };
    }

    private static string MergeJsonArrays(List<string> results)
    {
        var merged = new List<string>();
        foreach (var r in results)
        {
            var trimmed = r.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                merged.Add(trimmed[1..^1]);
            else
                merged.Add(trimmed);
        }
        return $"[{string.Join(",", merged)}]";
    }

    private static string MergeHtml(List<string> results)
    {
        var bodies = results.Select(r =>
        {
            var start = r.IndexOf("<body>", StringComparison.OrdinalIgnoreCase);
            var end = r.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (start >= 0 && end > start)
                return r[(start + 6)..end];
            return r;
        });

        var firstWithHead = results.FirstOrDefault(r =>
            r.Contains("<head>", StringComparison.OrdinalIgnoreCase)) ?? "";

        var headStart = firstWithHead.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        var headEnd = firstWithHead.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var head = headStart >= 0 && headEnd > headStart
            ? firstWithHead[headStart..(headEnd + 7)]
            : "<head><meta charset='utf-8'></head>";

        return $"<!DOCTYPE html><html>{head}<body>{string.Join("\n", bodies)}</body></html>";
    }

    private static string MergeCsv(List<string> results)
    {
        var lines = new List<string>();
        for (int i = 0; i < results.Count; i++)
        {
            var chunkLines = results[i].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (i == 0)
                lines.AddRange(chunkLines);
            else
                lines.AddRange(chunkLines.Skip(1));
        }
        return string.Join("\n", lines);
    }

    private static string MergeMarkdown(List<string> results)
    {
        return string.Join("\n\n---\n\n", results);
    }

    private static string MergeText(List<string> results)
    {
        return string.Join("\n\n", results);
    }
}
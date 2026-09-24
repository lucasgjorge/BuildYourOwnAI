using System.Text.RegularExpressions;

namespace BuildYourOwnAI.Api.Common;

public static partial class TextChunker
{
    public const int ChunkSize = 1000;
    public const int Overlap = 200;

    /// <summary>
    /// Collapses whitespace, then cuts windows of up to <see cref="ChunkSize"/> chars, preferring to end after a space
    /// in the second half of the window. Each chunk after the first starts with the last <see cref="Overlap"/> chars
    /// of the previous one.
    /// </summary>
    public static IReadOnlyList<string> Split(string text)
    {
        var normalized = Whitespace().Replace(text, " ").Trim();
        var chunks = new List<string>();
        if (normalized.Length == 0) return chunks;

        var start = 0;
        while (true)
        {
            var end = Math.Min(start + ChunkSize, normalized.Length);
            if (end < normalized.Length)
            {
                var lastSpace = normalized.LastIndexOf(' ', end - 1, end - start - ChunkSize / 2);
                if (lastSpace > start) end = lastSpace + 1;
            }

            chunks.Add(normalized[start..end]);
            if (end == normalized.Length) return chunks;
            start = end - Overlap;
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

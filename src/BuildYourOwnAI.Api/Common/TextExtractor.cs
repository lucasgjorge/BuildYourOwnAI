using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace BuildYourOwnAI.Api.Common;

public static class TextExtractor
{
    public static readonly string[] SupportedExtensions = [".pdf", ".txt", ".md"];

    public static bool IsSupported(string fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the plain text of the file, or an empty string when none can be extracted.</summary>
    public static string Extract(string fileName, byte[] content)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return DecodeText(content);

        try
        {
            using var pdf = PdfDocument.Open(content);
            var text = new StringBuilder();
            foreach (var page in pdf.GetPages())
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
            return text.ToString();
        }
        catch (Exception)
        {
            // A corrupt or encrypted PDF has no extractable text as far as the caller is concerned.
            return string.Empty;
        }
    }

    private static string DecodeText(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}

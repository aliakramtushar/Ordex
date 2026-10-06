using System.Globalization;
using System.Text;
using Ordex.Core.Abstractions;

namespace Ordex.Web.Infrastructure;

public static class FormFileExtensions
{
    /// <summary>Hands an uploaded file to the service layer without leaking ASP.NET types into it.</summary>
    public static UploadedFile? ToUploadedFile(this IFormFile? file) =>
        file is null || file.Length == 0
            ? null
            : new UploadedFile(file.FileName, file.Length, file.OpenReadStream);
}

public static class HttpRequestExtensions
{
    /// <summary>True for fetch/XHR calls that expect JSON back.</summary>
    public static bool WantsJson(this HttpRequest request) =>
        request.Headers.XRequestedWith == "XMLHttpRequest" ||
        request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
}

/// <summary>Builds RFC 4180 CSV that Excel opens correctly (UTF-8 BOM keeps Bangla text intact).</summary>
public sealed class CsvBuilder
{
    private readonly StringBuilder _sb = new();

    public CsvBuilder Row(params object?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0) _sb.Append(',');
            _sb.Append(Escape(values[i]));
        }

        _sb.Append("\r\n");
        return this;
    }

    public byte[] ToBytes() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_sb.ToString())).ToArray();

    private static string Escape(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        // Stop spreadsheet formula injection (=, +, -, @ at the start of a cell).
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]) && value is string)
            text = "'" + text;

        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ordex.Core.Abstractions;
using Ordex.Core.Common;

namespace Ordex.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Physical folder that is served as /uploads (set to wwwroot/uploads by the web app).</summary>
    public string RootPath { get; set; } = string.Empty;

    public string RequestPath { get; set; } = "/uploads";

    public int MaxImageSizeMb { get; set; } = 5;
}

/// <summary>
/// Saves images to disk. Defences:
///  • size limit
///  • real file type taken from the file's first bytes (magic number), not its name
///  • random file names (no user text reaches the file system)
///  • deletes only inside the uploads folder (no path traversal)
/// </summary>
public sealed partial class LocalFileStorage(
    IOptions<FileStorageOptions> options,
    ILogger<LocalFileStorage> logger) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    public async Task<ServiceResult<string>> SaveImageAsync(UploadedFile file, string folder, CancellationToken ct = default)
    {
        var maxBytes = _options.MaxImageSizeMb * 1024L * 1024L;
        if (file.Length <= 0 || file.Length > maxBytes)
            return ServiceResult<string>.Fail(string.Format(Messages.ImageTooLarge, _options.MaxImageSizeMb));

        if (!SafeFolder().IsMatch(folder))
            throw new ArgumentException("Invalid upload folder.", nameof(folder));

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        var extension = DetectImageExtension(header.AsSpan(0, read));
        if (extension is null)
            return ServiceResult<string>.Fail(Messages.ImageInvalid);

        var relativeFolder = Path.Combine(folder.Replace('/', Path.DirectorySeparatorChar), DateTime.UtcNow.ToString("yyyyMM"));
        var physicalFolder = Path.Combine(_options.RootPath, relativeFolder);
        Directory.CreateDirectory(physicalFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(physicalFolder, fileName);

        await using (var output = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await output.WriteAsync(header.AsMemory(0, read), ct);
            await input.CopyToAsync(output, ct);
        }

        var webPath = $"{_options.RequestPath.TrimEnd('/')}/{relativeFolder.Replace(Path.DirectorySeparatorChar, '/')}/{fileName}";
        return ServiceResult<string>.Ok(webPath);
    }

    public void Delete(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath))
            return;

        var prefix = _options.RequestPath.TrimEnd('/') + "/";
        if (!webPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return;

        var root = Path.GetFullPath(_options.RootPath);
        var relative = webPath[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));

        // Never touch anything outside the uploads folder.
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete file {Path}", fullPath);
        }
    }

    private static string? DetectImageExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return ".jpg";

        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";

        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
            return ".webp";

        return null;
    }

    [GeneratedRegex(@"^[a-z0-9]+(/[a-z0-9]+)*$")]
    private static partial Regex SafeFolder();
}

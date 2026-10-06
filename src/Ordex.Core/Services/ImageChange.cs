using Ordex.Core.Abstractions;
using Ordex.Core.Common;

namespace Ordex.Core.Services;

/// <summary>
/// Keeps files and database in sync when a record's image changes.
///   1. Prepare()  – upload the new file BEFORE the database transaction.
///   2. Commit()   – after the transaction succeeds, delete the old file.
///   3. Rollback() – if the transaction fails, delete the file we just uploaded.
/// The current path always comes from the database row, never from the form,
/// so a tampered form can't make us delete someone else's file.
/// </summary>
public sealed class ImageChange
{
    private readonly IFileStorage _storage;
    private readonly string? _uploaded;
    private readonly string? _obsolete;

    private ImageChange(IFileStorage storage, string? finalPath, string? uploaded, string? obsolete)
    {
        _storage = storage;
        FinalPath = finalPath;
        _uploaded = uploaded;
        _obsolete = obsolete;
    }

    /// <summary>The image path to save on the record.</summary>
    public string? FinalPath { get; }

    public static async Task<ServiceResult<ImageChange>> PrepareAsync(
        IFileStorage storage, string? currentPath, bool remove, UploadedFile? upload, string folder)
    {
        if (upload is { Length: > 0 })
        {
            var saved = await storage.SaveImageAsync(upload, folder);
            if (!saved.Succeeded)
                return ServiceResult<ImageChange>.Fail(saved.Error!);

            return ServiceResult<ImageChange>.Ok(new ImageChange(storage, saved.Value, saved.Value, currentPath));
        }

        return remove
            ? ServiceResult<ImageChange>.Ok(new ImageChange(storage, null, null, currentPath))
            : ServiceResult<ImageChange>.Ok(new ImageChange(storage, currentPath, null, null));
    }

    public void Commit() => _storage.Delete(_obsolete);

    public void Rollback() => _storage.Delete(_uploaded);
}

using gradyn_api_2.Models;

namespace gradyn_api_2.Services.DAL;

public interface INextcloudClient
{
    /// <summary>
    /// Downloads a file as a stream and surfaces the server ETag (if provided)
    /// </summary>
    Task<NextcloudDownloadResult> DownloadAsync(
        string remotePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an ETag for a file
    /// </summary>
    Task<string?> GetETagAsync(string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file, optionally validating an E-Tag
    /// </summary>
    Task<NextcloudUploadResult> UploadAsync(
        string remotePath,
        Stream content,
        string? ifMatchETag = null,
        bool failIfExists = false,
        string? contentType = null,
        CancellationToken cancellationToken = default);
}
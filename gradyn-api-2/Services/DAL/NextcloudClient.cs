using gradyn_api_2.Models;

namespace gradyn_api_2.Services.DAL;

public sealed class NextcloudClient(IConfiguration configuration) : INextcloudClient
{
    
    private readonly HttpClient _http = new();
    private readonly Uri _webDavBase = new(config, $"remote.php/dav/files/{configuration["Nextcloud"]}");

    /// <summary>
    /// Downloads a file as a stream and surfaces the server ETag (if provided)
    /// </summary>
    public async Task<NextcloudDownloadResult> DownloadAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        var fileUri = new Uri(_webDavBase, remotePath);

        // ResponseHeadersRead so we don't buffer the whole file
        using var request = new HttpRequestMessage(HttpMethod.Get, fileUri);

        var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        string? etag = response.Headers.ETag?.ToString();

        var contentLength = response.Content.Headers.ContentLength;
        var contentType = response.Content.Headers.ContentType?.ToString();

        // best-effort, often null unless server sets Content-Disposition
        var cd = response.Content.Headers.ContentDisposition;
        var fileName =
            cd?.FileNameStar ??
            cd?.FileName?.Trim('"') ??
            Path.GetFileName(remotePath);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new NextcloudDownloadResult(
            response,
            stream,
            etag,
            contentLength,
            contentType,
            fileName);
    }

    /// <summary>
    /// Get an ETag for a file
    /// </summary>
    public async Task<string?> GetETagAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        var fileUri = new Uri(_webDavBase, remotePath);
        using var request = new HttpRequestMessage(HttpMethod.Head, fileUri);

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return response.Headers.ETag?.ToString();
    }
    
    /// <summary>
    /// Uploads a file, optionally validating an E-Tag
    /// </summary>
    public async Task<NextcloudUploadResult> UploadAsync(
        string remotePath,
        Stream content,
        string? ifMatchETag = null,
        bool failIfExists = false,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var fileUri = new Uri(_webDavBase, remotePath);

        using var request = new HttpRequestMessage(HttpMethod.Put, fileUri);

        request.Content = new StreamContent(content);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        if (!string.IsNullOrWhiteSpace(ifMatchETag))
        {
            request.Headers.IfMatch.ParseAdd(ifMatchETag);
        }
        else if (failIfExists)
        {
            request.Headers.IfNoneMatch.ParseAdd("*");
        }

        var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            throw new ETagConflictException();
        }

        response.EnsureSuccessStatusCode();

        var newEtag = response.Headers.ETag?.ToString();
        var length = response.Content.Headers.ContentLength;

        return new NextcloudUploadResult(newEtag, length);
    }
}
namespace gradyn_api_2.Models;

public sealed class NextcloudDownloadResult : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;

    internal NextcloudDownloadResult(
        HttpResponseMessage response,
        Stream contentStream,
        string? etag,
        long? contentLength,
        string? contentType,
        string? fileName)
    {
        _response = response;
        Stream = contentStream;
        ETag = etag;
        ContentLength = contentLength;
        ContentType = contentType;
        FileName = fileName;
    }

    public Stream Stream { get; }
    public string? ETag { get; }
    public long? ContentLength { get; }
    public string? ContentType { get; }
    public string? FileName { get; }

    public async ValueTask DisposeAsync()
    {
        // disposing the response disposes the content stream
        _response.Dispose();
        await ValueTask.CompletedTask;
    }
}
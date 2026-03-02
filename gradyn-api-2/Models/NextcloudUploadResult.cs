namespace gradyn_api_2.Models;

public sealed record NextcloudUploadResult(
    string? ETag,
    long? ContentLength);
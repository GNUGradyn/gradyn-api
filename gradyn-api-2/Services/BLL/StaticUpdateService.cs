using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using gradyn_api_2.Models;

namespace gradyn_api_2.Services.BLL;

public class StaticUpdateService : IStaticUpdateService
{
    private readonly HttpClient _githubClient;
    private readonly IConfiguration _config;

    public StaticUpdateService(IConfiguration config)
    {
        _config = config;
        _githubClient = new HttpClient();
        _githubClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.GetValue<string>("GithubToken"));
        _githubClient.DefaultRequestHeaders.Add("User-Agent", "Gradyn API (https://github.com/GNUGradyn/gradyn-api/)");
    }

    public async Task PerformStaticUpdateAsync(string webhookId)
    {
        var webhookParameters = _config.GetSection($"StaticUpdates:{webhookId}").Get<WebhookParameters>();

        if (webhookParameters == null)
        {
            throw new KeyNotFoundException("No such webhook");
        }
        
        // phase 1: lookup artifact URL
        
        var metaRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{webhookParameters.RepoOwner}/{webhookParameters.RepoName}/actions/artifacts");
        metaRequest.Headers.Add("Accept", "application/vnd.github+json");
        metaRequest.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        using var metaResponse = await _githubClient.SendAsync(metaRequest);
        metaResponse.EnsureSuccessStatusCode();
        var rawMetaResult = await metaResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GithubArtifactList>(rawMetaResult);

        if (result == null)
        {
            throw new JsonException("Could not deserialize artifact list");
        }
        
        var latestMatch = result.Artifacts
            .Where(x => x.Name == webhookParameters.ArtifactName)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (latestMatch == null)
        {
            throw new KeyNotFoundException("No such artifact");
        }
        
        // phase 2: stream-download artifact
        var fileRequest = new HttpRequestMessage(HttpMethod.Get, latestMatch.ArchiveDownloadUrl);
        using var fileResponse = await _githubClient.SendAsync(fileRequest, HttpCompletionOption.ResponseHeadersRead);
        fileResponse.EnsureSuccessStatusCode();

        await using var stream = await fileResponse.Content.ReadAsStreamAsync();
        await using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.Combine(webhookParameters.Destination, entry.FullName);

            // prevent zip slip vulnerability
            var fullPath = Path.GetFullPath(destinationPath);
            if (!fullPath.StartsWith(Path.GetFullPath(webhookParameters.Destination), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Insecure zip operation prevented");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var entryStream = await entry.OpenAsync();
            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await entryStream.CopyToAsync(fileStream);
        }
    }
}
using System.Text.Json.Serialization;

namespace gradyn_api_2.Models;

public class GithubArtifactList
{
    [JsonPropertyName("total_count")]
    public required int Count { get; set; }
    [JsonPropertyName("artifacts")]
    public required Artifacts[] Artifacts { get; set; }
}

public class Artifacts
{
    
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size_in_bytes")]
    public int SizeInBytes { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("archive_download_url")]
    public string ArchiveDownloadUrl { get; set; }

    [JsonPropertyName("expired")]
    public bool Expired { get; set; }

    [JsonPropertyName("digest")]
    public string Digest { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("workflow_run")]
    public WorkflowRun WorkflowRun { get; set; }
}

public class WorkflowRun
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("repository_id")]
    public int RepositoryId { get; set; }

    [JsonPropertyName("head_repository_id")]
    public int HeadRepositoryId { get; set; }

    [JsonPropertyName("head_branch")]
    public string HeadBranch { get; set; }

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; set; }
}

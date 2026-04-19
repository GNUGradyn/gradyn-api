namespace gradyn_api_2.Models;

public class WebhookParameters
{
    public string RepoOwner { get; set; }
    public string RepoName { get; set; }
    public string ArtifactName { get; set; }
    public string Destination { get; set; }
}
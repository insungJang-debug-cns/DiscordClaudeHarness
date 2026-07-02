using Octokit;

namespace DiscordClaudeHarness.Services;

public class GitHubPrService
{
    private readonly GitHubClient _client;

    public GitHubPrService(string personalAccessToken)
    {
        _client = new GitHubClient(new ProductHeaderValue("discord-claude-harness"))
        {
            Credentials = new Credentials(personalAccessToken)
        };
    }

    public async Task<string> CreatePullRequestAsync(
        string owner, string repoName, string headBranch, string baseBranch,
        string title, string body)
    {
        var newPr = new NewPullRequest(title, headBranch, baseBranch) { Body = body };
        var pr = await _client.PullRequest.Create(owner, repoName, newPr);
        return pr.HtmlUrl;
    }
}

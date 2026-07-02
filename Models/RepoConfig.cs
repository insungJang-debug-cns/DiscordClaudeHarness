namespace DiscordClaudeHarness.Models;

/// <summary>
/// repos.json 한 항목에 대응. Discord에서 "repo:my-project-1" 처럼 참조할 때 사용.
/// </summary>
public class RepoConfig
{
    public string Name { get; set; } = string.Empty;
    public string GitUrl { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
}

public class RepoConfigRoot
{
    public List<RepoConfig> Repos { get; set; } = new();
}

/// <summary>
/// 하나의 "/fix" 요청 처리 결과를 담는 클래스. Discord 응답에 사용.
/// </summary>
public class TaskResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string? PullRequestUrl { get; set; }
    public string? DiffSummary { get; set; }
}

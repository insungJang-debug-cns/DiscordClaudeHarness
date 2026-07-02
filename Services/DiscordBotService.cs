using Discord;
using Discord.WebSocket;
using DiscordClaudeHarness.Models;

namespace DiscordClaudeHarness.Services;

/// <summary>
/// 중간관리자 역할. Discord 슬래시 커맨드를 받아서
/// Git → Claude Code → Git push → PR 생성 → Discord 응답까지 전체 흐름을 조율.
/// </summary>
public class DiscordBotService
{
    private readonly DiscordSocketClient _client;
    private readonly string _botToken;
    private readonly List<RepoConfig> _repos;
    private readonly string _workspaceRoot;
    private readonly string _defaultBranch;

    private readonly GitOperations _git = new();
    private readonly ClaudeCodeRunner _claude;
    private readonly GitHubPrService _github;

    public DiscordBotService(
        string botToken,
        List<RepoConfig> repos,
        string workspaceRoot,
        string defaultBranch,
        string claudeExecutablePath,
        int claudeTimeoutMinutes,
        string githubToken)
    {
        _botToken = botToken;
        _repos = repos;
        _workspaceRoot = workspaceRoot;
        _defaultBranch = defaultBranch;
        _claude = new ClaudeCodeRunner(claudeExecutablePath, claudeTimeoutMinutes);
        _github = new GitHubPrService(githubToken);

        var config = new DiscordSocketConfig { GatewayIntents = GatewayIntents.Guilds };
        _client = new DiscordSocketClient(config);

        _client.Log += msg => { Console.WriteLine(msg.ToString()); return Task.CompletedTask; };
        _client.Ready += OnReadyAsync;
        _client.SlashCommandExecuted += OnSlashCommandExecutedAsync;
    }

    public async Task StartAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();
        await Task.Delay(Timeout.Infinite);
    }

    private async Task OnReadyAsync()
    {
        // "/fix repo:my-project-1 task:로그인 버그 수정" 형태의 슬래시 커맨드 등록
        var fixCommand = new SlashCommandBuilder()
            .WithName("fix")
            .WithDescription("지정한 repo에서 Claude에게 작업을 시키고 PR을 생성합니다.")
            .AddOption("repo", ApplicationCommandOptionType.String, "repos.json에 등록된 repo 이름", isRequired: true)
            .AddOption("task", ApplicationCommandOptionType.String, "Claude에게 시킬 작업 설명", isRequired: true);

        await _client.CreateGlobalApplicationCommandAsync(fixCommand.Build());
        Console.WriteLine("봇이 준비되었습니다. /fix 명령을 사용하세요.");
    }

    private async Task OnSlashCommandExecutedAsync(SocketSlashCommand command)
    {
        if (command.CommandName != "fix") return;

        await command.DeferAsync(); // "생각 중..." 표시. 작업이 오래 걸리므로 먼저 응답 지연 처리

        var repoName = (string)command.Data.Options.First(o => o.Name == "repo").Value;
        var task = (string)command.Data.Options.First(o => o.Name == "task").Value;

        var repoConfig = _repos.FirstOrDefault(r => r.Name == repoName);
        if (repoConfig == null)
        {
            await command.FollowupAsync($"❌ repos.json에 `{repoName}` 이(가) 등록되어 있지 않습니다.");
            return;
        }

        try
        {
            var result = await ProcessFixRequestAsync(repoConfig, task);

            if (result.Success)
            {
                await command.FollowupAsync(
                    $"✅ 작업 완료!\n**브랜치**: {result.BranchName}\n**PR**: {result.PullRequestUrl}\n```\n{result.DiffSummary}\n```");
            }
            else
            {
                await command.FollowupAsync($"⚠️ {result.Message}");
            }
        }
        catch (Exception ex)
        {
            await command.FollowupAsync($"❌ 오류 발생: {ex.Message}");
        }
    }

    /// <summary>
    /// 핵심 오케스트레이션: clone/pull → 브랜치 생성 → Claude 실행 → diff 확인 → commit/push → PR 생성
    /// </summary>
    private async Task<TaskResult> ProcessFixRequestAsync(RepoConfig repoConfig, string task)
    {
        var localPath = Path.Combine(_workspaceRoot, repoConfig.Name);
        var branchName = $"claude/fix-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        await _git.EnsureRepoUpToDateAsync(repoConfig.GitUrl, localPath, _defaultBranch);
        await _git.CreateBranchAsync(localPath, branchName);

        await _claude.RunTaskAsync(task, localPath);

        if (!await _git.HasChangesAsync(localPath))
        {
            return new TaskResult { Success = false, Message = "Claude가 변경사항을 만들지 않았습니다. 작업 설명을 더 구체적으로 적어보세요." };
        }

        var diffSummary = await _git.GetDiffSummaryAsync(localPath);
        await _git.CommitAndPushAsync(localPath, branchName, $"Claude: {task}");

        var prUrl = await _github.CreatePullRequestAsync(
            repoConfig.Owner, repoConfig.RepoName, branchName, _defaultBranch,
            title: task,
            body: $"Discord 요청으로 자동 생성된 PR입니다.\n\n**요청 내용**: {task}\n\n```\n{diffSummary}\n```");

        return new TaskResult
        {
            Success = true,
            BranchName = branchName,
            PullRequestUrl = prUrl,
            DiffSummary = diffSummary
        };
    }
}

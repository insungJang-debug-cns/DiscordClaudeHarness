using System.Diagnostics;
using System.Text;

namespace DiscordClaudeHarness.Services;

/// <summary>
/// git CLI를 서브프로세스로 실행해서 clone/branch/commit/push를 처리.
/// WinForm에서 Process.Start로 외부 exe 실행하던 패턴과 동일합니다.
/// </summary>
public class GitOperations
{
    private async Task<(int ExitCode, string StdOut, string StdErr)> RunGitAsync(string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("git 프로세스를 시작하지 못했습니다.");

        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdOut.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stdErr.AppendLine(e.Data); };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, stdOut.ToString(), stdErr.ToString());
    }

    /// <summary>repo가 로컬에 없으면 clone, 있으면 최신화(pull).</summary>
    public async Task EnsureRepoUpToDateAsync(string gitUrl, string localPath, string defaultBranch)
    {
        if (!Directory.Exists(Path.Combine(localPath, ".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            var (code, _, err) = await RunGitAsync($"clone \"{gitUrl}\" \"{localPath}\"", Path.GetDirectoryName(localPath)!);
            if (code != 0) throw new InvalidOperationException($"git clone 실패: {err}");
        }
        else
        {
            var (code, _, err) = await RunGitAsync($"checkout {defaultBranch}", localPath);
            if (code != 0) throw new InvalidOperationException($"checkout 실패: {err}");

            (code, _, err) = await RunGitAsync("pull", localPath);
            if (code != 0) throw new InvalidOperationException($"git pull 실패: {err}");
        }
    }

    public async Task CreateBranchAsync(string localPath, string branchName)
    {
        var (code, _, err) = await RunGitAsync($"checkout -b {branchName}", localPath);
        if (code != 0) throw new InvalidOperationException($"브랜치 생성 실패: {err}");
    }

    /// <summary>Claude가 수정한 내용이 있는지 확인. 변경 없으면 false.</summary>
    public async Task<bool> HasChangesAsync(string localPath)
    {
        var (_, stdOut, _) = await RunGitAsync("status --porcelain", localPath);
        return !string.IsNullOrWhiteSpace(stdOut);
    }

    public async Task<string> GetDiffSummaryAsync(string localPath)
    {
        var (_, stdOut, _) = await RunGitAsync("diff --stat", localPath);
        return stdOut.Trim();
    }

    public async Task CommitAndPushAsync(string localPath, string branchName, string commitMessage)
    {
        var (code, _, err) = await RunGitAsync("add .", localPath);
        if (code != 0) throw new InvalidOperationException($"git add 실패: {err}");

        (code, _, err) = await RunGitAsync($"commit -m \"{commitMessage}\"", localPath);
        if (code != 0) throw new InvalidOperationException($"git commit 실패: {err}");

        (code, _, err) = await RunGitAsync($"push -u origin {branchName}", localPath);
        if (code != 0) throw new InvalidOperationException($"git push 실패: {err}");
    }
}

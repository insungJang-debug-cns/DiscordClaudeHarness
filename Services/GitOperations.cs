using System.Diagnostics;
using System.Text;

namespace DiscordClaudeHarness.Services;

/// <summary>
/// git CLI를 서브프로세스로 실행해서 clone/branch/commit/push를 처리.
/// WinForm에서 Process.Start로 외부 exe 실행하던 패턴과 동일합니다.
/// </summary>
public class GitOperations
{
    private async Task<(int ExitCode, string StdOut, string StdErr)> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

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
            var (code, _, err) = await RunGitAsync(Path.GetDirectoryName(localPath)!, "clone", gitUrl, localPath);
            if (code != 0) throw new InvalidOperationException($"git clone 실패: {err}");
        }
        else
        {
            var (code, _, err) = await RunGitAsync(localPath, "checkout", defaultBranch);
            if (code != 0) throw new InvalidOperationException($"checkout 실패: {err}");

            (code, _, err) = await RunGitAsync(localPath, "pull");
            if (code != 0) throw new InvalidOperationException($"git pull 실패: {err}");
        }
    }

    public async Task CreateBranchAsync(string localPath, string branchName)
    {
        var (code, _, err) = await RunGitAsync(localPath, "checkout", "-b", branchName);
        if (code != 0) throw new InvalidOperationException($"브랜치 생성 실패: {err}");
    }

    /// <summary>Claude가 수정한 내용이 있는지 확인. 변경 없으면 false.</summary>
    public async Task<bool> HasChangesAsync(string localPath)
    {
        var (_, stdOut, _) = await RunGitAsync(localPath, "status", "--porcelain");
        return !string.IsNullOrWhiteSpace(stdOut);
    }

    public async Task<string> GetDiffSummaryAsync(string localPath)
    {
        var (_, stdOut, _) = await RunGitAsync(localPath, "diff", "--stat");
        return stdOut.Trim();
    }

    /// <summary>빌드 실패 등으로 커밋하지 않을 변경사항을 되돌려서, 다음 실행이 깨끗한 상태에서 시작하도록 함.</summary>
    public async Task DiscardChangesAsync(string localPath)
    {
        await RunGitAsync(localPath, "reset", "--hard");
        await RunGitAsync(localPath, "clean", "-fd");
    }

    public async Task CommitAndPushAsync(string localPath, string branchName, string commitMessage)
    {
        var (code, _, err) = await RunGitAsync(localPath, "add", ".");
        if (code != 0) throw new InvalidOperationException($"git add 실패: {err}");

        (code, _, err) = await RunGitAsync(localPath, "commit", "-m", commitMessage);
        if (code != 0) throw new InvalidOperationException($"git commit 실패: {err}");

        (code, _, err) = await RunGitAsync(localPath, "push", "-u", "origin", branchName);
        if (code != 0) throw new InvalidOperationException($"git push 실패: {err}");
    }
}

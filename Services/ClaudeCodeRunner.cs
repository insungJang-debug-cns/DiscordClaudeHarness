using System.Diagnostics;
using System.Text;

namespace DiscordClaudeHarness.Services;

/// <summary>
/// "claude -p {task}" 형태로 Claude Code CLI를 실행.
/// repo 폴더 안에 CLAUDE.md가 있으면 Claude가 자동으로 읽어서 컨텍스트로 활용합니다.
/// </summary>
public class ClaudeCodeRunner
{
    private readonly string _executablePath;
    private readonly int _timeoutMinutes;

    public ClaudeCodeRunner(string executablePath, int timeoutMinutes)
    {
        _executablePath = executablePath;
        _timeoutMinutes = timeoutMinutes;
    }

    /// <summary>
    /// task: 사람이 입력한 자연어 지시사항 (예: "로그인 버그 수정해줘")
    /// workingDirectory: 작업 대상 repo의 로컬 경로 (해당 repo의 CLAUDE.md를 Claude가 자동 인식)
    /// </summary>
    public async Task<string> RunTaskAsync(string task, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _executablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // --print(-p): 비대화형으로 한 번 실행하고 결과만 받기
        // --output-format json: 파싱하기 쉬운 JSON으로 결과 수신
        // --dangerously-skip-permissions: 터미널이 없어 승인 프롬프트에 응답할 수 없으므로 필수
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(task);
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--dangerously-skip-permissions");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("claude 프로세스를 시작하지 못했습니다. CLI 설치 여부를 확인하세요.");

        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdOut.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stdErr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeout = TimeSpan.FromMinutes(_timeoutMinutes);
        var completed = process.WaitForExit((int)timeout.TotalMilliseconds);

        if (!completed)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Claude Code 실행이 {_timeoutMinutes}분을 초과해 중단되었습니다.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Claude Code 실행 실패 (exit {process.ExitCode}): {stdErr}");
        }

        return stdOut.ToString();
    }
}

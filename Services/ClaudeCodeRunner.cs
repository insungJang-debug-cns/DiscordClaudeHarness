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
            // --print(-p): 비대화형으로 한 번 실행하고 결과만 받기
            // --output-format json: 파싱하기 쉬운 JSON으로 결과 수신
            Arguments = $"-p \"{EscapeForShell(task)}\" --output-format json",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("claude 프로세스를 시작하지 못했습니다. CLI 설치 여부를 확인하세요.");

        var stdOut = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdOut.AppendLine(e.Data); };
        process.BeginOutputReadLine();

        var timeout = TimeSpan.FromMinutes(_timeoutMinutes);
        var completed = process.WaitForExit((int)timeout.TotalMilliseconds);

        if (!completed)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Claude Code 실행이 {_timeoutMinutes}분을 초과해 중단되었습니다.");
        }

        return stdOut.ToString();
    }

    private static string EscapeForShell(string input) => input.Replace("\"", "\\\"");
}

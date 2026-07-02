using System.Diagnostics;
using System.Text;

namespace DiscordClaudeHarness.Services;

/// <summary>
/// Claude가 변경사항을 만든 뒤, push/PR 생성 전에 repo별 빌드 명령을 실행해 검증.
/// buildCommand는 repos.json에서 온 신뢰된 값이므로 cmd.exe로 그대로 실행(임의 셸 구문 허용).
/// </summary>
public class BuildRunner
{
    public async Task<(bool Success, string Output)> RunAsync(string buildCommand, string workingDirectory, int timeoutMinutes)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(buildCommand);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("빌드 프로세스를 시작하지 못했습니다.");

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeout = TimeSpan.FromMinutes(timeoutMinutes);
        var completed = process.WaitForExit((int)timeout.TotalMilliseconds);

        if (!completed)
        {
            process.Kill(entireProcessTree: true);
            return (false, $"빌드가 {timeoutMinutes}분을 초과해 중단되었습니다.");
        }

        return (process.ExitCode == 0, output.ToString());
    }
}

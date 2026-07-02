# Discord-Claude Harness — 작업 상태 메모

이 파일은 세션/기기 간 연속성을 위한 기록입니다. 새 세션(특히 다른 컴퓨터)에서 시작할 때 이 파일을 먼저 읽고 현재 상태를 파악하세요.

## 이 프로젝트가 뭔지
Discord 슬래시 커맨드(`/fix repo:... task:...`)로 Claude Code CLI를 실행해 등록된 GitHub repo를 수정하고, 빌드 검증 후 PR을 자동 생성하는 C#(.NET 8) 콘솔 앱. [README.md](README.md) 참고.

## 지금까지 세팅 완료된 것 (원본 컴퓨터 기준)
- Claude CLI: VSCode 확장 번들 exe를 `%USERPROFILE%\.local\bin\claude.exe`로 복사, PATH 등록, `appsettings.json`에 절대경로로 명시
- Discord Bot Token, GitHub PAT(Fine-grained, All repositories, Contents+PR RW) — `appsettings.json`에 입력 (이 파일은 `.gitignore`로 제외되어 git엔 없음, 컴퓨터마다 별도 입력 필요)
- git push 인증: `git credential approve`로 github.com용 PAT를 Credential Manager에 저장 (컴퓨터마다 별도로 다시 해야 함)
- `repos.json`: `DAPLibManager`(defaultBranch: master), `MouseDebouncer`(defaultBranch: main) 등록, 각각 `buildCommand: "dotnet build"` 지정
- Workspace RootPath: `C:\ClaudeHarness\repos` (harness가 여기에 별도로 clone, 기존 `E:\Projects\...` 작업 폴더와 무관)
- 바탕화면 바로가기: `bin\Debug\net8.0\DiscordClaudeHarness.exe`를 직접 가리킴 (빌드하면 자동 갱신됨)

## 코드에 있는 주요 설계/버그 수정 이력
- `GitOperations`/`ClaudeCodeRunner`: 프로세스 인자는 문자열 이어붙이기 대신 **`ProcessStartInfo.ArgumentList` 사용** (커밋 메시지에 따옴표/한글 있으면 pathspec 에러 나던 버그 수정), stdout/stderr 인코딩 UTF-8 명시
- `ClaudeCodeRunner`: `--dangerously-skip-permissions` 필수 (터미널 없는 무인 실행이라 권한 승인 프롬프트에 응답 불가 → 이거 없으면 Claude가 파일을 못 씀), exit code 체크 + stderr 캡처 추가
- `DiscordBotService`: `SlashCommandExecuted` 핸들러를 `Task.Run`으로 분리 (안 하면 게이트웨이 하트비트 스레드를 막음 — "blocking the gateway task" 경고)
- `RepoConfig`에 레포별 `DefaultBranch`, `BuildCommand` 추가 (레포마다 기본 브랜치/빌드 명령이 다를 수 있음)
- push 전 `dotnet build` 자동 실행 → 실패하면 `git reset --hard` + `clean -fd`로 되돌리고 PR 생성 안 함 (Discord로 빌드 로그 전달)
- `repo` 슬래시 옵션에 `repos.json` 기반 자동완성(choices) 등록 (최대 25개, 레포 추가 시 봇 재시작 필요)

## 알려진 미해결 사항
- **역할/채널 제한 미구현** — `AllowedRoleIds`/`CommandChannelId`는 appsettings.json에 있지만 코드에서 검증 안 함. 사용자가 "개인 채널이라 필요 없다"고 확인함 (2026-07-03 기준). 나중에 공유 서버로 확장하면 반드시 추가할 것.
- 상시 실행은 지금 콘솔 창(바탕화면 바로가기) 방식 — 로그아웃/재부팅하면 꺼짐. Windows 서비스/작업 스케줄러 전환은 아직 안 함.

## 다음 컴퓨터(회사 노트북)에서 할 일
1. 이 repo `git clone https://github.com/insungJang-debug-cns/DiscordClaudeHarness.git`
2. `.NET 8 SDK`, `git` 설치 확인
3. Claude Code CLI 설치 + `claude login`으로 그 계정 인증 (exe만 복사해선 안 됨, 로그인 세션 필요)
4. `appsettings.json.example`을 복사해 `appsettings.json` 생성, Discord Bot Token / GitHub PAT 입력 (메일로 전달받은 값 사용하거나 새로 발급)
5. `git credential approve`로 github.com PAT 등록 (push 인증, 컴퓨터별 필요)
6. `C:\ClaudeHarness\repos` 디렉터리는 없으면 앱이 알아서 만듦 (첫 `/fix` 실행 시 대상 repo도 자동 clone됨 — 미리 clone 불필요)
7. `dotnet build` 후 실행해서 Discord 로그인/커맨드 등록 확인

## repos.json 관리 대상
- `DAPLibManager` (master)
- `MouseDebouncer` (main)
- 새 레포 추가 시: `repos.json`에 항목 추가(defaultBranch, buildCommand 포함) → 재빌드 → 봇 재시작 (자동완성 갱신용)

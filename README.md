# Discord-Claude Harness

Discord를 중간관리자로 두고, Claude Code CLI로 GitHub 프로젝트를 수정/디버깅/PR 생성까지 자동화하는 C# 콘솔 앱 스켈레톤입니다.

## 사전 준비

1. **.NET 8 SDK** 설치 (https://dotnet.microsoft.com/download)
2. **git** 설치 및 PATH 등록 확인 (`git --version`)
3. **Claude Code CLI** 설치 확인 (`claude --version`)
4. **Discord Bot 생성**
   - https://discord.com/developers/applications 에서 New Application
   - Bot 탭 → Reset Token → 토큰 복사 (`appsettings.json`의 `Discord:BotToken`에 입력)
   - OAuth2 → URL Generator에서 `bot`, `applications.commands` 스코프 선택 후 서버에 초대
5. **GitHub Personal Access Token** 발급
   - Settings → Developer settings → Personal access tokens
   - `repo` 권한 필요 (private repo까지 다루려면)
   - `appsettings.json`의 `GitHub:PersonalAccessToken`에 입력

## 설정

- `appsettings.json`: 봇 토큰, GitHub 토큰, 워크스페이스 경로 등
- `repos.json`: 관리할 repo 목록 (Discord에서 `repo:이름`으로 참조)
- 각 대상 repo 루트에 `CLAUDE.md.example`을 참고해 `CLAUDE.md`를 만들어두면, 프로젝트별 규칙을 Claude가 자동 인식합니다.

## 실행

```powershell
dotnet restore
dotnet run
```

정상 실행되면 콘솔에 "봇이 준비되었습니다. /fix 명령을 사용하세요." 가 출력됩니다.

## 사용법

Discord 채널에서:

```
/fix repo:my-project-1 task:로그인 버그 수정해줘
```

처리 흐름:
1. repo 최신화 (clone 또는 pull)
2. 새 브랜치 생성 (`claude/fix-YYYYMMDD-HHmmss`)
3. Claude Code CLI 실행 → 코드 수정
4. 변경사항 있으면 commit & push
5. GitHub PR 자동 생성
6. Discord에 PR 링크 회신

## 다음 단계로 확장하고 싶다면

- **권한 제한**: `OnSlashCommandExecutedAsync`에서 명령 실행자의 Role 체크 추가
- **동시 실행 방지**: repo별 락(lock) 또는 작업 큐 도입 (여러 명이 동시에 같은 repo에 `/fix` 요청 시 충돌 방지)
- **테스트/빌드 자동 검증**: `ClaudeCodeRunner` 실행 후, git diff 확인 전에 빌드/테스트 명령 실행 단계 추가
- **Worker Service로 전환**: 24시간 안정적으로 돌리려면 `dotnet new worker` 템플릿 기반으로 이전 후 Windows 서비스 또는 소규모 VPS에 배포

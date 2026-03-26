<p align="center">
  <h1 align="center">UniCLI</h1>
  <p align="center">
    Unity Editor 자동화를 위한 커맨드라인 인터페이스
  </p>
  <p align="center">
    <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
    <img src="https://img.shields.io/badge/Unity-6-blue?logo=unity" alt="Unity 6">
    <img src="https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet" alt=".NET 8.0">
  </p>
  <p align="center">
    <a href="README.md">English</a> | <b>한국어</b>
  </p>
</p>

---

UniCLI는 AI 에이전트(Claude 등)와 스크립트가 Unity Editor를 단일 바이너리로 제어할 수 있게 합니다. MCP 브릿지, Node.js, 설정 파일 없이 — `unicli <명령>` 하나면 됩니다.

## 아키텍처

```
Claude / 스크립트 → unicli.exe → Named Pipe → Unity Editor
```

- **`unicli.exe`** — .NET 8 CLI 클라이언트. Named Pipe로 명령 전달.
- **Unity 패키지** — 에디터 내부 Named Pipe 서버. PID 기반 파이프 이름, 도메인 리로드 생존.
- **인스턴스 레지스트리** — `~/.unicli/instances/*.json`. 여러 에디터, 자동 탐색.

## 저장소 구조

```
UniCLI/
├── com.inonego.uni-cli/   ← Unity Editor 플러그인 (UPM)
│   └── Plugins/           ← InoCLI.dll, InoIPC.dll (netstandard2.1)
├── cli/                   ← CLI 클라이언트 (.NET 8)
└── lib/                   ← 서브모듈 (InoCLI, InoIPC)
```

## 설치

### 1. Unity 플러그인

Unity에서: **Window > Package Manager > + > Add package from git URL...** (둘 다 설치)

```
https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua
https://github.com/inonego-unity/UniCLI.git?path=/com.inonego.uni-cli
```

또는 `Packages/manifest.json`에 직접 추가:
```json
{
  "dependencies": {
    "com.inonego.uni-lua": "https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua",
    "com.inonego.uni-cli": "https://github.com/inonego-unity/UniCLI.git?path=/com.inonego.uni-cli"
  }
}
```

> **UniLua 필수** — UniCLI와 함께 또는 먼저 설치하세요. 없으면 `eval lua`를 사용할 수 없습니다.

### 2. Claude 스킬 (선택)

Unity에서: **Window > UniCLI Settings > Claude Skill > Sync Skill**

**Auto Sync**(기본 켜짐) 활성화 시 도메인 리로드마다 `.claude/skills/inonego-uni-cli/`에 자동 복사됩니다.

### 3. CLI 클라이언트

```bash
cd UniCLI/cli
dotnet publish -c Release -o bin/publish
```

`bin/publish`를 PATH에 추가하거나, `unicli.exe`의 전체 경로를 사용하세요.

## 빠른 시작

```bash
# 연결 확인
unicli ping

# C# 코드 실행
unicli eval cs 'return 1+1;'

# Lua 실행
unicli eval lua 'return CS.UnityEngine.Application.dataPath'

# 씬 목록
unicli scene list

# GameObject 생성
unicli go create Player --primitive cube

# 파일에서 코드 파이프
cat script.cs | unicli eval cs -
```

## 명령어

### eval — 코드 실행

```bash
unicli eval cs '<code>'              # C# (CSharpCodeProvider, 런타임 컴파일)
unicli eval cs '<code>' --using Ns   # 추가 using
unicli eval lua '<code>'             # Lua (UniLua, 인터프리터 — 더 빠른 실행)
cat file.cs | unicli eval cs -       # stdin 파이프 (POSIX "-")
```

### scene — 씬 관리

```bash
unicli scene list                    # 열린 씬 목록
unicli scene new                     # 새 씬 생성
unicli scene open <path>             # 씬 열기 [--additive]
unicli scene save                    # 저장 [--id <handle>] [--all]
unicli scene close                   # 닫기 [--id <handle>] [--save]
unicli scene root                    # 루트 GameObject 목록 [--id <handle>]
unicli scene active                  # 활성 씬 조회/설정 [--id <handle>]
```

### go — GameObject

```bash
unicli go create <name>              # 생성 [--primitive] [--parent <id>]
unicli go active <id> [on|off]       # 활성 상태 조회/설정
unicli go parent <id> [parent_id]    # 부모 조회/설정 [--null]
unicli go tag <id> [tag]             # 태그 조회/설정
unicli go layer <id> [layer]         # 레이어 조회/설정
unicli go scene <id> [handle]        # 씬 조회/설정
unicli go children <id>              # 자식 목록 [--recursive]
```

### object — 오브젝트 조작

```bash
unicli object instantiate <id>       # 복제 [--parent <id>] [--name]
unicli object destroy <id>           # 삭제
unicli object ping <id>              # 에디터에서 하이라이트
unicli object select <id...>         # 선택
unicli object name <id> [name]       # 이름 조회/설정
```

### asset — 에셋 관리

```bash
unicli asset import <path>           # 에셋 임포트
unicli asset mkdir <path>            # 폴더 생성
unicli asset rm <path>               # 에셋 삭제
unicli asset mv <src> <dst>          # 에셋 이동
unicli asset cp <src> <dst>          # 에셋 복사
unicli asset rename <path> <name>    # 에셋 이름 변경
unicli asset refresh                 # AssetDatabase 새로고침
unicli asset save                    # 에셋 저장 (--all 또는 --id <id>)
```

### editor — 에디터 제어

```bash
unicli editor play                   # 플레이 모드 시작
unicli editor stop                   # 플레이 모드 종료
unicli editor pause                  # 일시정지 토글
unicli editor step                   # 한 프레임 진행
unicli editor undo / redo            # 실행 취소/다시 실행
unicli editor state                  # 상태 조회 (playing, compiling 등)
unicli editor menu exec <path>       # 메뉴 항목 실행
unicli editor menu list              # 메뉴 항목 목록
unicli editor window list            # 열린 윈도우 목록
unicli editor window focus <id>      # 윈도우 포커스
unicli editor window close <id>      # 윈도우 닫기
unicli editor modal                  # 네이티브 모달 감지
unicli editor modal click "<button>" # 모달 버튼 클릭
unicli editor sdb                    # MonoDebug용 SDB 디버거 포트 확인
```

### console — 콘솔 로그

```bash
unicli console                       # 로그 읽기
unicli console clear                 # 버퍼 비우기
```

### search — Unity 검색

```bash
unicli search '<query>'              # 검색 (예: 't:Material', 'Player')
```

### capture / record — 화면 캡처

```bash
unicli capture game                  # 게임 뷰 캡처 [--path] [--scale]
unicli capture scene                 # 씬 뷰 캡처
unicli capture window <id>           # 윈도우 캡처
unicli record start                  # 녹화 시작 [--path] [--fps] [--duration]
unicli record stop                   # 녹화 중지
```

### prefab — 프리팹 조작

```bash
unicli prefab load <path>            # 편집용 로드
unicli prefab unload <root_id>       # 언로드
unicli prefab save <id> <path>       # 프리팹으로 저장
unicli prefab apply <id>             # 오버라이드 적용
unicli prefab revert <id>            # 오버라이드 되돌리기
unicli prefab unpack <id>            # 프리팹 인스턴스 해제
```

### package — 패키지 관리

```bash
unicli package list                  # 설치된 패키지 목록
unicli package install <id>          # 설치 (이름 또는 git URL)
unicli package rm <id>               # 제거
```

### test — 테스트 실행

```bash
unicli test run                      # 테스트 실행 [--mode edit|play] → job_id
unicli test list                     # 테스트 목록 [--mode edit|play] → job_id
```

### build — 프로젝트 빌드

```bash
unicli build                         # 빌드 [--target] [--path] [--run] → job_id
```

### poll — 비동기 작업 상태

```bash
unicli poll <job_id>                 # 작업 상태 조회
```

### wait — 조건 대기

```bash
unicli wait <condition>              # 조건 대기 [--timeout <초>]
```

> 조건: `not_compiling`, `not_playing`, `compiling`, `playing`
> 참고: `editor play`, `editor stop`, `asset refresh`는 기본적으로 자동 대기합니다. `--no-wait`로 건너뛸 수 있습니다.

### ping — 연결 확인

```bash
unicli ping
# {"success":true,"result":{"pipe":"unicli-1234","project":"MyGame","unity":"6000.3.7f1","platform":"StandaloneWindows64"}}
```

## 글로벌 옵션

| 옵션 | 설명 |
|------|------|
| `--pipe <name>` | Named Pipe 이름 (자동 탐색 대신 직접 지정) |
| `--project <name>` | 프로젝트 이름으로 선택 (부분 문자열 매칭) |
| `--pretty` | JSON 포맷팅 출력 |
| `--timeout <초>` | 연결/대기 타임아웃 |
| `--no-wait` | 도메인 리로드 명령의 자동 대기 건너뛰기 |
| `--help` | 도움말 |

**파이프 해석 순서**: `--pipe` > `UNICLI_PIPE` 환경변수 > 인스턴스 레지스트리 > 파이프 탐색

## 출력 형식

모든 명령은 JSON을 반환합니다:

```json
{"success":true,"result":...}
{"success":false,"error":{"code":"...","message":"..."}}
```

## 설정

Unity Editor에서 **Window > UniCLI Settings** 메뉴:

- **Auto-Start**: 에디터 시작 시 서버 자동 실행
- **Enabled**: 마스터 토글

## 커스텀 명령

InoCLI의 `[CLICommand]` 어트리뷰트로 직접 명령을 추가할 수 있습니다:

```csharp
using InoCLI;

namespace MyProject
{
   public static class MyTools
   {
      [CLICommand("my_tools", "hello", description = "Say hello")]
      public static object Hello(CommandArgs args)
      {
         string name = args[0];
         return new { message = $"Hello, {name}!" };
      }
   }
}
```

```bash
unicli my_tools hello World
# {"success":true,"result":{"message":"Hello, World!"}}
```

## 의존성

| 의존성 | 용도 | 라이선스 |
|--------|------|---------|
| [InoCLI](https://github.com/inonego/InoCLI) | CLI 프레임워크 (파서 + 커맨드 레지스트리) | MIT |
| [InoIPC](https://github.com/inonego/InoIPC) | IPC 프레임워크 (Named Pipe + 프레임 프로토콜) | MIT |

## 라이선스

[MIT](LICENSE)

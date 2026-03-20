<p align="center">
  <h1 align="center">UniCLI</h1>
  <p align="center">
    Command-line interface for Unity Editor automation
  </p>
  <p align="center">
    <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
    <img src="https://img.shields.io/badge/Unity-6-blue?logo=unity" alt="Unity 6">
    <img src="https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet" alt=".NET 8.0">
  </p>
  <p align="center">
    <b>English</b> | <a href="README.ko.md">한국어</a>
  </p>
</p>

---

UniCLI lets AI agents (Claude, etc.) and scripts control the Unity Editor through a single binary. No MCP bridge, no Node.js, no config files — just `unicli <command>`.

## Architecture

```
Claude / Script → unicli.exe → TCP → Unity Editor
```

- **`unicli.exe`** — .NET 8 CLI client. Thin TCP proxy.
- **Unity Package** — TCP server inside the editor. Auto-discovers ports, survives domain reloads.
- **Instance Registry** — `~/.unicli/instances/*.json`. Multiple editors, auto-discovery.

## Repository Structure

```
UniCLI/
├── com.inonego.uni-cli/   ← Unity Editor Plugin (UPM)
└── cli/                   ← CLI Client (.NET 8)
```

## Installation

### 1. Unity Plugin

In Unity: **Window > Package Manager > + > Add package from git URL...** (install both)

```
https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua
https://github.com/inonego-unity/UniCLI.git?path=/com.inonego.uni-cli
```

Or add directly to `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.inonego.uni-lua": "https://github.com/inonego-unity/UniLua.git?path=com.inonego.uni-lua",
    "com.inonego.uni-cli": "https://github.com/inonego-unity/UniCLI.git?path=/com.inonego.uni-cli"
  }
}
```

> **UniLua is required** — install it before or alongside UniCLI. Without it, `eval lua` will not be available.
>
> OpenUPM support is planned. Once available: `openupm add com.inonego.uni-cli`

### 2. Claude Skill (Optional)

In Unity: **Window > UniCLI Settings > Claude Skill > Sync Skill**

Or enable **Auto Sync** (default on) to automatically copy skill files to `.claude/skills/inonego-uni-cli/` on every domain reload.

### 3. CLI Client

```bash
cd UniCLI/cli
dotnet publish -c Release -o bin/publish
```

Add `bin/publish` to your PATH, or use the full path to `unicli.exe`.

## Quick Start

```bash
# Check connectivity
unicli ping

# Evaluate C# code
unicli eval cs 'return 1+1;'

# Evaluate Lua
unicli eval lua 'return CS.UnityEngine.Application.dataPath'

# List scenes
unicli scene list

# Create a GameObject
unicli go create Player --primitive cube

# Pipe code from file
cat script.cs | unicli eval cs -
```

## Commands (16 Groups)

### eval — Code Evaluation

```bash
unicli eval cs '<code>'              # C# (CSharpCodeProvider, compiles at runtime)
unicli eval cs '<code>' --using Ns   # with extra using
unicli eval lua '<code>'             # Lua (UniLua, interpreted — faster execution)
cat file.cs | unicli eval cs -       # stdin pipe (POSIX "-")
```

> **Lua is faster** — C# requires runtime compilation on each call, while Lua is interpreted and executes instantly. Use Lua for quick queries, C# for complex operations that need full .NET API access.

### scene — Scene Management

```bash
unicli scene list                    # list open scenes
unicli scene new                     # create new scene
unicli scene open <path>             # open scene [--additive]
unicli scene save                    # save [--id <handle>] [--all]
unicli scene close                   # close [--id <handle>] [--save]
unicli scene root                    # list root GameObjects [--id <handle>]
unicli scene active                  # get/set active scene [--id <handle>]
```

### go — GameObject

```bash
unicli go create <name>              # create [--primitive] [--parent <id>]
unicli go active <id> [on|off]       # get/set active
unicli go parent <id> [parent_id]    # get/set parent [--null]
unicli go tag <id> [tag]             # get/set tag
unicli go layer <id> [layer]         # get/set layer
unicli go scene <id> [handle]        # get/set scene
unicli go children <id>              # list children [--recursive]
```

### object — Object Operations

```bash
unicli object instantiate <id>       # clone [--parent <id>] [--name]
unicli object destroy <id>           # destroy
unicli object ping <id>              # highlight in editor
unicli object select <id...>         # set selection
unicli object name <id> [name]       # get/set name
```

### asset — Asset Management

```bash
unicli asset import <path>           # import asset
unicli asset mkdir <path>            # create folder
unicli asset rm <path>               # delete asset
unicli asset mv <src> <dst>          # move asset
unicli asset cp <src> <dst>          # copy asset
unicli asset rename <path> <name>    # rename asset
unicli asset refresh                 # refresh AssetDatabase
unicli asset save                    # save assets (--all or --id <id>)
```

### editor — Editor Control

```bash
unicli editor play                   # enter play mode
unicli editor stop                   # exit play mode
unicli editor pause                  # toggle pause
unicli editor step                   # step one frame
unicli editor undo / redo            # undo/redo
unicli editor state                  # get state (playing, compiling, etc.)
unicli editor menu exec <path>       # execute menu item
unicli editor menu list              # list menu items
unicli editor window list            # list open windows
unicli editor window focus <id>      # focus window
unicli editor window close <id>      # close window
unicli editor modal                  # detect native modal dialog
unicli editor modal click "<button>" # click modal button (e.g. "Save", "Don't Save")
```

> Modal dialogs (e.g. "Save Scene?") block the main thread. `editor modal` detects them via Win32 API without needing the main thread. Commands that trigger a modal automatically return a `MODAL` error with button info.

### console — Console Logs

```bash
unicli console                       # read logs
unicli console clear                 # clear buffer
```

### search — Unity Search

```bash
unicli search '<query>'              # search (e.g. 't:Material', 'Player')
```

### capture / record — Screen Capture

```bash
unicli capture game                  # capture game view [--path] [--scale]
unicli capture scene                 # capture scene view
unicli capture window <id>           # capture window by instance_id
unicli record start                  # start recording [--path] [--fps] [--duration]
unicli record stop                   # stop recording
```

### prefab — Prefab Operations

```bash
unicli prefab load <path>            # load for editing
unicli prefab unload <root_id>       # unload (root_id from load)
unicli prefab save <id> <path>       # save as prefab
unicli prefab apply <id>             # apply overrides
unicli prefab revert <id>            # revert overrides
unicli prefab unpack <id>            # unpack instance
```

### package — Package Management

```bash
unicli package list                  # list installed packages
unicli package install <id>          # install (name or git URL)
unicli package rm <id>               # remove
```

### test / build / poll / wait — Async Operations

```bash
unicli test run                      # run tests [--mode edit|play] → job_id
unicli test list                     # list tests [--mode edit|play] → job_id
unicli build                         # build [--target] [--path] [--run] → job_id
unicli poll <job_id>                 # poll job status
unicli wait <condition>              # wait for condition [--timeout <s>]
```

> Wait conditions: `not_compiling`, `not_playing`, `compiling`, `playing`

### ping — Connectivity

```bash
unicli ping
# {"success":true,"result":{"port":18960,"project":"MyGame","unity":"6000.3.7f1","platform":"StandaloneWindows64"}}
```

## Global Options

| Option | Description |
|--------|-------------|
| `--port <n>` | Server port (overrides auto-discovery) |
| `--project <name>` | Select Unity project by name (substring match) |
| `--pretty` | Pretty-print JSON output |
| `--timeout <s>` | Connection/wait timeout in seconds |
| `--help` | Show help |

**Port resolution order**: `--port` > `UNICLI_PORT` env var > instance registry > default (18960)

## Output Format

All commands return JSON:

```json
{"success":true,"result":...}
{"success":false,"error":{"code":"...","message":"..."}}
```

## Configuration

Open **Window > UniCLI Settings** in the Unity Editor:

- **Port**: Base TCP server port (default: 18960, auto-increments if in use)
- **Auto-Start**: Start server on editor launch
- **Enabled**: Master toggle

## Custom Commands

Add your own commands using the attribute pattern:

```csharp
using inonego.UniCLI.Attribute;
using inonego.UniCLI.Core;

namespace MyProject
{
   [CLIGroup("my_tools", "My custom tools")]
   public class MyToolsGroup
   {
      [CLICommand("hello", "Say hello")]
      public static object Hello(CommandArgs args)
      {
         string name = args.Arg(0);
         return new { message = $"Hello, {name}!" };
      }
   }
}
```

```bash
unicli my_tools hello World
# {"success":true,"result":{"message":"Hello, World!"}}
```

## License

[MIT](com.inonego.uni-cli/LICENSE)

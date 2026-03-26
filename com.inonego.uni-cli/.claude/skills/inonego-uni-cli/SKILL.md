---
name: inonego-uni-cli
description: Unity Editor CLI tool. Use when performing Unity editor operations — scene management, GameObject manipulation, asset operations, code evaluation, and more.
user-invocable: false
---

# UniCLI

`unicli <command> [args...] [--options]` — all commands return JSON.

## Global Options

| Option | Description |
|--------|-------------|
| `--pipe <name>` | Named Pipe name (overrides auto-discovery) |
| `--project <name>` | Select Unity project by name (substring match) |
| `--pretty` | Pretty-print JSON output |
| `--timeout <s>` | Connection/wait timeout in seconds |

Pipe resolution: `--pipe` > `UNICLI_PIPE` env > instance registry (`~/.unicli/instances/`) > pipe discovery

## Output Format

```json
{"success":true,"result":...}
{"success":false,"error":{"code":"...","message":"..."}}
```

Error codes: `INVALID_ARGS`, `INTERNAL_ERROR`, `COMPILE_ERROR`, `RUNTIME_ERROR`, `UNKNOWN_GROUP`, `UNKNOWN_COMMAND`, `MODAL`

Use `jq` to extract specific fields and reduce output tokens:
```bash
unicli scene root | jq '.result[].name'
unicli scene root | jq '.result[] | select(.tag=="Player") | .instance_id'
```

## Patterns

- **Get/Set**: omit value → get, provide → set
- **IDs**: positional if required, `--id` if optional. Negative IDs work as-is.
- **Async**: test/build return `job_id` → `unicli poll <job_id>`
- **Auto-wait**: `editor play`, `editor stop`, `asset refresh` automatically wait for completion (domain reload safe). Use `--no-wait` to skip.

---

## ping

```bash
unicli ping
```
Returns: `{"pipe":"unicli-1234","project":"MyGame","unity":"6000.3.7f1","platform":"StandaloneWindows64"}`

---

## eval — Code Evaluation

```bash
unicli eval lua '<code>'                 # Lua — preferred (no compilation, fast)
unicli eval cs '<code>'                  # C# — for full .NET API, generics, etc.
unicli eval cs '<code>' --using <ns>     # add using namespace (repeatable)
cat file | unicli eval cs -              # stdin pipe (POSIX `-` convention)
```

**Prefer Lua** for simple queries and property access. Use C# only when Lua can't do it (generics, LINQ, complex .NET API).
Both use `return` for output. C# requires `;`, Lua does not.

Lua accesses C# via `CS.` prefix:
```bash
unicli eval lua 'return CS.UnityEngine.Application.dataPath'
unicli eval lua 'return CS.UnityEngine.SceneManagement.SceneManager.GetActiveScene().name'
unicli eval lua 'return CS.UnityEditor.EditorApplication.isPlaying'
```

Quoting: `'...'` for code with `"`, `"..."` for code with `!` (escape `"` → `\"`).

---

## scene — Scene Management

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `list` | | | List open scenes |
| `new` | | `--setup`, `--mode` | Create new scene |
| `open` | `<path>` | `--additive` | Open scene |
| `save` | | `--id <handle>`, `--all`, `--path` | Save scene |
| `close` | | `--id <handle>`, `--save` | Close scene |
| `root` | | `--id <handle>` | List root GameObjects |
| `active` | | `--id <handle>` | Get or set active scene |

---

## go — GameObject

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `create` | `<name>` | `--primitive <type>`, `--parent <id>` | Create GameObject |
| `active` | `<id> [on\|off]` | `--hierarchy` | Get/set active state |
| `parent` | `<id> [parent_id]` | `--null` | Get/set parent |
| `tag` | `<id> [tag]` | | Get/set tag |
| `layer` | `<id> [layer]` | | Get/set layer |
| `scene` | `<id> [handle]` | | Get/set scene |
| `children` | `<id>` | `--recursive` | List children |

Primitives: `cube`, `sphere`, `capsule`, `cylinder`, `plane`, `quad`

---

## object — Object Operations

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `instantiate` | `<id>` | `--parent <id>`, `--name <name>` | Clone object |
| `destroy` | `<id>` | | Destroy object |
| `ping` | `<id>` | | Highlight in editor |
| `select` | `<id...>` | | Set editor selection |
| `name` | `<id> [name]` | | Get/set name |

---

## asset — Asset Management

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `import` | `<path>` | | Import asset |
| `mkdir` | `<path>` | | Create folder |
| `rm` | `<path>` | | Delete asset |
| `mv` | `<src> <dst>` | | Move asset |
| `cp` | `<src> <dst>` | | Copy asset |
| `rename` | `<path> <name>` | | Rename asset |
| `refresh` | | | Refresh AssetDatabase |
| `save` | | `--all` or `--id <id>` | Save assets |

After editing .cs files: `unicli asset refresh` (auto-waits for compilation)

---

## editor — Editor Control

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `play` | | | Enter play mode |
| `stop` | | | Exit play mode |
| `pause` | | | Toggle pause |
| `step` | | | Step one frame |
| `undo` | | | Undo |
| `redo` | | | Redo |
| `state` | | | Get state (`playing`, `paused`, `compiling`) |
| `menu exec` | `<path>` | | Execute menu item |
| `menu list` | | | List menu items |
| `window list` | | | List open windows |
| `window focus` | `<id>` | | Focus window |
| `window close` | `<id>` | | Close window |
| `modal` | | | Detect native modal (Win32) |
| `modal click` | `<button>` | | Click modal button |
| `sdb` | | | Get SDB debugger port for MonoDebug |

Modal is auto-detected during commands. Manual: `unicli editor modal` → `unicli editor modal click "Save"`

---

## console — Console Logs

| Command | Description |
|---------|-------------|
| (default) | Read log buffer (includes `CompileError` type) |
| `clear` | Clear log buffer |

---

## search — Unity Search

```bash
unicli search '<query>'
```
Common queries: `t:Material`, `t:Prefab`, `t:Scene`, `t:Script`, `Player`

---

## capture / record

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `capture` | `game\|scene\|window <id>` | `--path`, `--scale` | Screenshot |
| `record start` | | `--path`, `--fps`, `--duration` | Start recording |
| `record stop` | | | Stop recording |

`capture game` and `record` require play mode.

---

## prefab

| Command | Args | Description |
|---------|------|-------------|
| `load` | `<path>` | Load prefab for editing |
| `unload` | `<root_id>` | Unload prefab contents |
| `save` | `<id> <path>` | Save as prefab |
| `apply` | `<id>` | Apply overrides |
| `revert` | `<id>` | Revert overrides |
| `unpack` | `<id>` | Unpack instance |

---

## package

| Command | Args | Description |
|---------|------|-------------|
| `list` | | List installed packages |
| `install` | `<id>` | Install (name or git URL) |
| `rm` | `<id>` | Remove package |

---

## test / build / poll

```bash
unicli test run [--mode edit|play]      # → job_id
unicli test list [--mode edit|play]     # → job_id
unicli build --path Builds/Game.exe [--target <t>] [--run]   # → job_id
unicli poll <job_id>                    # → {"status":"running|completed|failed","result":...}
```

Build auto-saves open scenes. Windows builds require `.exe` in path.

---

## wait

```bash
unicli wait <condition> [--timeout <s>]
```
Conditions: `not_compiling`, `not_playing`, `compiling`, `playing`

Client-side polling (survives domain reload).

---

## Serialization

All results serialized via `ResultSerializer`:

| C# Type | JSON |
|---------|------|
| null | `null` |
| Primitive/string | `42`, `"hello"` |
| Scene | `{"name","path","handle","active","dirty"}` |
| GameObject | `{"instance_id","name","type","active","tag","layer","scene"}` |
| Component | `{"instance_id","name","type","game_object"}` |
| UnityEngine.Object | `{"instance_id","name","type"}` |
| IDictionary | `{"key":"value"}` |
| IEnumerable | `[...]` |
| Other | Newtonsoft JSON |

**snake_case keys**. Unity objects return identification only — for full data use `eval` with `JsonUtility.ToJson()`.

---

## Debugging (MonoDebug)

Use with [MonoDebug](https://github.com/inonego-unity/MonoDebug) for runtime debugging without adding Debug.Log to code — set breakpoints, inspect variables, evaluate expressions at runtime.

```bash
# Start
unicli editor sdb                    # get SDB debugger port
monodebug attach <port>              # attach (run monodebug --help for details)

# After each debug session
monodebug flow continue              # resume execution
monodebug profile disable --all      # disable all debug profiles

# When completely done
monodebug detach                     # disconnect (requires Unity restart to reattach)
```

Requires: Edit > Preferences > External Tools > Editor Attaching enabled + Unity restart.

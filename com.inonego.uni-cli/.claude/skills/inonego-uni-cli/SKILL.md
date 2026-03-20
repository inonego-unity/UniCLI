---
name: inonego-uni-cli
description: Unity Editor CLI tool. Use when performing Unity editor operations — scene management, GameObject manipulation, asset operations, code evaluation, and more.
user-invocable: false
---

# UniCLI

`unicli <group> [command] [args...] [--options]` — all commands return JSON.

## Global Options

| Option | Description |
|--------|-------------|
| `--port <n>` | Server port (overrides auto-discovery) |
| `--project <name>` | Select Unity project by name (substring match) |
| `--pretty` | Pretty-print JSON output |
| `--timeout <s>` | Connection/wait timeout in seconds |

Port resolution: `--port` > `UNICLI_PORT` env > instance registry (`~/.unicli/instances/`) > 18960

## Output Format

```json
{"success":true,"result":...}
{"success":false,"error":{"code":"...","message":"..."}}
```

Error codes: `INVALID_ARGS`, `INTERNAL_ERROR`, `COMPILE_ERROR`, `RUNTIME_ERROR`, `UNKNOWN_GROUP`, `UNKNOWN_COMMAND`, `MODAL`

## Patterns

- **Get/Set**: omit value → get, provide → set
- **Stdin**: `cat file | unicli eval cs -` (POSIX `-`)
- **Async**: test/build → `job_id` → `poll <job_id>`
- **IDs**: positional if required, `--id` if optional. Negative IDs work as-is.
- **Modal**: auto-detected during commands. Manual: `editor modal` → `editor modal click "Save"`
- **Quoting**: `'...'` when code has `"`, `"..."` when code has `!` or special chars (escape inner `"` with `\"`).

---

## ping

```bash
unicli ping
```
Returns: `{"port":18960,"project":"MyGame","unity":"6000.3.7f1","platform":"StandaloneWindows64"}`

---

## eval — Code Evaluation

```bash
unicli eval lua '<code>'                 # Lua — preferred (no compilation, fast)
unicli eval cs '<code>'                  # C# — for full .NET API, generics, etc.
unicli eval cs '<code>' --using <ns>     # add using namespace (repeatable)
cat file | unicli eval cs -              # stdin pipe
```

**Prefer Lua** for simple queries and property access. Use C# only when Lua can't do it (generics, LINQ, complex .NET API).
Both use `return` for output. C# requires `;`, Lua does not.

Lua accesses C# via `CS.` prefix:
```bash
unicli eval lua 'return CS.UnityEngine.Application.dataPath'
unicli eval lua 'return CS.UnityEngine.SceneManagement.SceneManager.GetActiveScene().name'
unicli eval lua 'return CS.UnityEditor.EditorApplication.isPlaying'
```

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
| `record start` | | `--path`, `--fps`, `--duration` | Start recording (play mode only) |
| `record stop` | | | Stop recording |

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

## jq Examples

```bash
unicli scene root | jq '.result[].name'
unicli scene root | jq '[.result[] | select(.active==true)]'
unicli scene root | jq '.result[] | select(.tag=="Player")'
unicli package list | jq '[.result[] | select(.source=="Local")]'
unicli editor window list | jq '.result[].title'

# Chain: find → destroy
ID=$(unicli scene root | jq -r '.result[] | select(.name=="Player") | .instance_id')
unicli object destroy $ID
```

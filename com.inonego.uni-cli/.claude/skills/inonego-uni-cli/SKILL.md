---
name: inonego-uni-cli
description: Unity Editor CLI tool. Use when performing Unity editor operations — scene management, GameObject manipulation, asset operations, code evaluation, and UI Toolkit debugging.
user-invocable: false
---

# UniCLI

`unicli <command> [args...] [--options]` — all commands return JSON.

## Global Options

| Option | Description |
|--------|-------------|
| `--pipe <name>` | Named Pipe name (overrides auto-discovery) |
| `--project <str>` | Select Unity project. Substring match on `project_name` OR `project_path`. |
| `--pretty` | Pretty-print JSON output |
| `--timeout <s>` | Connection/wait timeout in seconds |

Pipe resolution: `--pipe` > `UNICLI_PIPE` env > instance registry (`~/.unicli/instances/`) > pipe discovery

**Multi-instance**: if 2+ Unity editors are open, auto-discovery errors with `AMBIGUOUS_INSTANCE` listing available pipes — always pass `--project` or `--pipe`.

`--project <str>` substring-matches `project_name` OR `project_path` (case-insensitive). When multiple match, an exact `project_name` match wins if unique; otherwise narrow with a path fragment or pin with `--pipe <id>`.

Verify with `unicli ping` that `.result.project` matches your intent before debugging — many "bridge bug" reports are really "attached to wrong project".

`UNICLI_AUTO_PICK=1` restores legacy most-recent-timestamp auto-pick for CI.

```bash
ls ~/.unicli/instances/*.json | xargs -I{} jq -r '"\(.project_name) → \(.pipe)"' {}
```

## Output Format

```json
{"success":true,"result":...}
{"success":false,"error":{"code":"...","message":"..."}}
```

Error codes: `INVALID_ARGS`, `INTERNAL_ERROR`, `COMPILE_ERROR`, `RUNTIME_ERROR`, `UNKNOWN_GROUP`, `UNKNOWN_COMMAND`, `MODAL`, `AMBIGUOUS_INSTANCE`, `CONNECT_FAILED`

## Use jq aggressively

All output is JSON — **always pipe through `jq`** to cut noise. `scene root`, `console`, `search` unfiltered can burn thousands of tokens.

```bash
unicli scene root | jq '.result[].name'                                     # extract field
unicli scene root | jq '.result | length'                                   # count
unicli scene root | jq '.result[] | select(.tag=="Player") | .instance_id'  # filter+project
unicli scene root | jq '.result[:5]'                                        # slice
unicli console --type Error | jq '.result[] | {type, message}'              # drop stacktrace
unicli console --type Error | jq -r '.result[].message'                     # raw strings
ID=$(unicli scene root | jq -r '.result[0].instance_id'); unicli object select $ID
OUT=$(unicli eval lua 'return ...'); echo "$OUT" | jq -e '.success' >/dev/null && echo "$OUT" | jq '.result'
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

```json
{
  "success": true,
  "result": {"pipe":"unicli-1234","project":"MyGame","unity":"6000.3.7f1","platform":"StandaloneWindows64"}
}
```

---

## eval — Code Evaluation

```bash
unicli eval lua '<code>'                 # Lua — preferred (no compilation, fast)
unicli eval cs '<code>'                  # C# — for full .NET API, generics, etc.
unicli eval cs '<code>' --using <ns>     # add using namespace (repeatable)
cat file | unicli eval cs -              # stdin pipe (POSIX `-` convention)
```

**Prefer Lua** — fast, no compilation, no subprocess.

Use `eval cs` only for generics, LINQ, or .NET APIs the Lua bridge can't reach.

Both use `return` for output. C# requires `;`, Lua doesn't.

> Older UniCLI builds hit Windows cmdline-length errors on `eval cs` in projects with many assemblies. Current builds route references via a response file, so this no longer occurs.

Lua accesses C# via `CS.` prefix:
```bash
unicli eval lua 'return CS.UnityEngine.Application.dataPath'
unicli eval lua 'return CS.UnityEditor.EditorApplication.isPlaying'
```

Quoting: `'...'` for code with `"`, `"..."` for code with `!` (escape `"` → `\"`).

### Lua ↔ C# bridge pitfalls

| Pattern | Behavior | Use instead |
|---------|----------|-------------|
| `arr[i]` on C# array | nil | `arr:GetValue(i)` (0-based) |
| `tr:GetChild(i)` | nil | `GetEnumerator` |
| `Selection.activeGameObject` | null | `Selection.activeTransform.gameObject` |
| generic `FindObjectsByType<T>` | unsupported | `FindObjectsByType(typeof(T), mode)` |
| `EditorUtility.InstanceIDToObject(id)` in play mode | nil | runtime traversal (`Camera.main`, `FindAnyObjectByType(Type)`) |

`typeof(x)` — global Lua function. Accepts `CS.*` type proxy / CLR instance / type-name string, returns `System.Type` (nil on failure). Use wherever C# API expects a Type.

```lua
-- C# array
for i = 0, arr.Length - 1 do local v = arr:GetValue(i) end

-- Transform children
local e = tr:GetEnumerator()
while e:MoveNext() do local c = e.Current end

-- Find all of a type (edit/play)
local t   = typeof(CS.UnityEngine.Camera)
local all = CS.UnityEngine.Object.FindObjectsByType(t, CS.UnityEngine.FindObjectsSortMode.None)
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

`object select` returns `{selected:[...], not_found:[...]}` — unresolved IDs are reported explicitly, not dropped silently.
```bash
unicli object select 12345 99999 | jq '.result'
```
```json
{"selected":[12345],"not_found":[99999]}
```

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
| (default) | Read console logs |
| `--type` | CompileError, Error, Warning, Log |
| `clear` | Clear console buffer |

```bash
unicli console                                       # All logs
unicli console --type CompileError                   # CompileError only
unicli console --type Error --type Warning           # Error + Warning
unicli console --type Error | jq '.result | length'  # Count errors
```

**Output format:** `type` (log type), `message` (log content), `stacktrace` (call stack array, if present)

```json
{
  "success": true,
  "result": [
    {
      "type": "Log",
      "message": "test message",
      "stacktrace": ["ClassName:Method ()", "OtherClass:Method (at Assets/File.cs:10)"]
    }
  ]
}
```

**Filter with jq**

```bash
unicli console | jq '.result | length'                                                       # Count all entries
unicli console | jq '.result[] | select(.message | contains("MyClass"))'                     # Filter by message text
unicli console --type Error | jq '.result[] | {type, message, stacktrace: .stacktrace[:3]}'  # First 3 frames
```

**Workflow** — Check compile errors after edits

```bash
unicli console clear
unicli asset refresh
unicli console --type CompileError
```

---

## search — Unity Search

```bash
unicli search '<query>'
```

Multiple providers (not asset-only):

| Prefix | Provider | Example |
|--------|----------|---------|
| `t:` | by Type | `t:Material`, `t:Prefab`, `t:Scene`, `t:Script` |
| `h:` | Scene **Hierarchy** (open scenes) | `h:Camera`, `h:t:Light` |
| `p:` | Project path | `p:Assets/Scenes` |
| `i:` | Index-backed general search | `i:Player` |

```bash
unicli search 'h:Camera' | jq -r '.result[].instance_id'   # scene GameObjects → IDs
unicli search 'h:t:Light' | jq '.result | length'          # count Light components
unicli search 't:Prefab Player'                            # Prefabs matching "Player"
```

For scene-hierarchy lookup, `h:` is usually more reliable than `FindObjectsByType` in Lua.

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
unicli test run [--mode edit|play]                           # → job_id
unicli test list [--mode edit|play]                          # → job_id
unicli build --path Builds/Game.exe [--target <t>] [--run]   # → job_id
unicli poll <job_id> # → {"status":"running|completed|failed","result":...}
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

---

## References

- [UITK.md](references/UITK.md) — UI Toolkit visual tree inspection, USS styling, and layout debugging. Use when inspecting or debugging UITK visual trees, stylesheets, or element layouts.

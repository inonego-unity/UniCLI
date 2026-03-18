# UniCLI Command Reference

## Global Options

| Option | Description |
|--------|-------------|
| `--port <n>` | Server port (overrides auto-discovery) |
| `--project <name>` | Select Unity project by name (substring match) |
| `--pretty` | Pretty-print JSON output |
| `--timeout <s>` | Connection/wait timeout in seconds |
| `--help` | Show help (returns JSON from server) |

Port resolution: `--port` > `UNICLI_PORT` env var > instance registry (`~/.unicli/instances/`) > default 18960

---

## ping

```bash
unicli ping
```
Returns: `{"port":18960,"project":"MyGame","unity":"6000.3.7f1","platform":"StandaloneWindows64"}`

---

## eval — Code Evaluation

```bash
unicli eval cs '<code>'                  # C# (runtime compilation)
unicli eval cs '<code>' --using <ns>     # add using namespace (repeatable)
unicli eval lua '<code>'                 # Lua (interpreted, faster)
cat file | unicli eval cs -              # stdin pipe
```

C# code must use `return` for output. Lua uses `return` directly.

Examples:
```bash
unicli eval cs 'return GameObject.Find("Player").name;'
unicli eval cs 'var go = new GameObject("Test"); return go.GetInstanceID();'
unicli eval cs --using UnityEditor.SceneManagement 'EditorSceneManager.SaveOpenScenes(); return null;'
unicli eval lua 'return CS.UnityEngine.Application.dataPath'
unicli eval lua 'return CS.UnityEngine.SceneManagement.SceneManager.GetActiveScene().name'
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

Scene object: `{"name":"...","path":"...","handle":<int>,"active":<bool>,"dirty":<bool>}`

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

Primitive types: `cube`, `sphere`, `capsule`, `cylinder`, `plane`, `quad`

GameObject object: `{"instance_id":<int>,"name":"...","type":"...","active":<bool>,"tag":"...","layer":<int>,"scene":<int>}`

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
| `modal` | | | Detect native modal dialog (Win32, no main thread) |
| `modal click` | `<button>` | | Click a button on the modal dialog |

State object: `{"playing":<bool>,"paused":<bool>,"compiling":<bool>}`

Window object: `{"instance_id":<int>,"type":"...","title":"..."}`

---

## console — Console Logs

| Command | Args | Description |
|---------|------|-------------|
| (default) | | Read log buffer |
| `clear` | | Clear log buffer |

Log entry: `{"type":"...","message":"...","stacktrace":"...","timestamp":"..."}`

---

## search — Unity Search

```bash
unicli search '<query>'
```

Result: `[{"id":"...","label":"...","description":"..."}]`

Common queries: `t:Material`, `t:Prefab`, `t:Scene`, `t:Script`, `Player`

---

## capture / record — Screen Capture

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `capture` | `game\|scene\|window <id>` | `--path`, `--scale` | Screenshot |
| `record start` | | `--path`, `--fps`, `--duration` | Start recording (play mode only) |
| `record stop` | | | Stop recording (play mode only) |

Result: `{"path":"...","width":<int>,"height":<int>}`

---

## prefab — Prefab Operations

| Command | Args | Description |
|---------|------|-------------|
| `load` | `<path>` | Load prefab for editing |
| `unload` | `<root_id>` | Unload prefab contents (root_id from load) |
| `save` | `<id> <path>` | Save as prefab |
| `apply` | `<id>` | Apply overrides |
| `revert` | `<id>` | Revert overrides |
| `unpack` | `<id>` | Unpack instance |

---

## package — Package Management

| Command | Args | Description |
|---------|------|-------------|
| `list` | | List installed packages |
| `install` | `<id>` | Install (name or git URL) |
| `rm` | `<id>` | Remove package |

Package object: `{"name":"...","version":"...","display_name":"...","source":"..."}`

---

## test — Test Management

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `run` | | `--mode edit\|play` | Run tests → `job_id` |
| `list` | | `--mode edit\|play` | List tests → `job_id` |

---

## build — Project Build

```bash
unicli build --target <target> --path Builds/Game.exe --run
```
Returns `job_id`. Poll with `unicli poll <job_id>`. Open scenes are auto-saved before building. Windows builds require `.exe` in path. Editor must be focused for build to start.

---

## poll — Poll Async Job

```bash
unicli poll <job_id>
```
Result: `{"status":"running|completed|failed","result":...}`

---

## wait — Wait for Condition

```bash
unicli wait <condition> [--timeout <s>]
```

Conditions: `not_compiling`, `not_playing`, `compiling`, `playing`

Runs client-side (survives domain reload). Polls `editor state` via TCP every 500ms.

Result: `{"condition":"not_compiling","elapsed":3200}`

---

## Custom Commands

```csharp
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
```

```bash
unicli my_tools hello World
```

---

## jq Examples

```bash
unicli scene root | jq '.result[].name'
unicli scene root | jq '[.result[] | select(.active==true)]'
unicli scene root | jq '.result[] | select(.tag=="Player")'
unicli scene list | jq '.result[] | select(.dirty==true)'
unicli package list | jq '[.result[] | select(.source=="Local")]'
unicli editor state | jq '.result.playing'
unicli editor window list | jq '.result[].title'
unicli eval cs 'return 1+1;' | jq '.result'

# Chain: find → destroy
ID=$(unicli scene root | jq -r '.result[] | select(.name=="Player") | .instance_id')
unicli object destroy $ID
```

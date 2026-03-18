---
name: unicli
description: Unity Editor CLI tool. Use when performing Unity editor operations — scene management, GameObject manipulation, asset operations, code evaluation, and more.
user-invocable: false
---

# UniCLI

`unicli <group> [command] [args...] [--options]` — all commands return JSON.

## Commands

```bash
unicli ping
unicli eval cs '<code>' / lua '<code>'       # Lua is faster (no compilation)
unicli scene list / new / open / save / close / root / active
unicli go create|active|parent|tag|layer|scene|children <id> [value]
unicli object instantiate|destroy|ping|select|name <id> [value]
unicli asset import|mkdir|rm|mv|cp|rename <path> / refresh / save --all
unicli editor play|stop|pause|step|undo|redo|state
unicli editor menu exec|list / window list|focus|close
unicli editor modal [click <button>]         # detect/dismiss Win32 modals
unicli console [clear] / search '<query>'
unicli capture game|scene|window <id> [--path] [--scale]
unicli record start|stop                     # play mode only
unicli prefab load|unload|save|apply|revert|unpack
unicli package list / install|rm <id>
unicli test run|list [--mode] / build [--target] [--run] / poll <job_id>
unicli wait <condition> [--timeout <s>]      # not_compiling|playing|...
```

## Patterns

- **Get/Set**: omit value → get, provide → set
- **Stdin**: `cat file | unicli eval cs -` (POSIX `-`)
- **Async**: test/build → `job_id` → `poll <job_id>`
- **IDs**: positional if required, `--id` if optional. Negative IDs work as-is.
- **Modal**: auto-detected during commands. Manual: `editor modal` → `editor modal click "Save"`
- **Options**: `--port`, `--project`, `--pretty`, `--timeout`
- **Port**: `--port` > `UNICLI_PORT` env > instance registry > 18960

## Output

```json
{"success":true,"result":...}
{"success":false,"error":{"code":"...","message":"..."}}
```

## References

- [Full command reference](references/commands.md)
- [Serialization rules](references/serialization.md)

# UniCLI Result Serialization

All command results are serialized to JSON via `ResultSerializer`. The serialization chain processes return values in order:

## Serialization Chain

| Priority | C# Type | Method | JSON Result |
|----------|---------|--------|-------------|
| 1 | `null` | `JValue.CreateNull()` | `null` |
| 2 | `JToken` | Pass through | (as-is) |
| 3 | Primitive, string, decimal, DateTime | `JValue` | `42`, `"hello"`, `3.14` |
| 4 | Enum | `JValue(ToString())` | `"PlayMode"` |
| 5 | `Scene` | Custom JObject | See below |
| 6 | `GameObject` | Custom JObject | See below |
| 7 | `Component` | Custom JObject | See below |
| 8 | `UnityEngine.Object` | Base JObject | See below |
| 9 | `IDictionary` | JObject (recursive) | `{"key":"value"}` |
| 10 | `IEnumerable` | JArray (recursive) | `[1,2,3]` |
| 11 | `[Serializable]` | JsonUtility → fallback Newtonsoft | Depends on type |
| 12 | Unity namespace | JsonUtility → fallback Newtonsoft | Depends on type |
| 13 | Fallback | Newtonsoft + CLIJsonConverter | `{"name":"Test"}` |

## Unity Type Formats

### Scene
```json
{
  "name": "GameScene",
  "path": "Assets/Scenes/GameScene.unity",
  "handle": -1294,
  "active": true,
  "dirty": false
}
```

### GameObject
```json
{
  "instance_id": 12345,
  "name": "Player",
  "type": "UnityEngine.GameObject",
  "active": true,
  "tag": "Player",
  "layer": 0,
  "scene": -1294
}
```

### Component
```json
{
  "instance_id": 67890,
  "name": "Rigidbody",
  "type": "UnityEngine.Rigidbody",
  "game_object": 12345
}
```

### UnityEngine.Object (base)
```json
{
  "instance_id": 99999,
  "name": "MyMaterial",
  "type": "UnityEngine.Material"
}
```

## Key Rules

- **snake_case keys** — all JSON keys use snake_case (`instance_id`, `game_object`, `full_name`)
- **UnityEngine.Object subclasses return identification only** — no internal serialized data. For full serialization, use `eval` with `JsonUtility.ToJson(obj)`.
- **Nested Unity objects in anonymous/POCO types** — handled by `CLIJsonConverter` which intercepts Unity types during Newtonsoft serialization and routes through `ResultSerializer.Serialize()`.
- **Serialization runs on main thread** — required because Unity properties like `go.scene` need main thread access.

## Error Format

```json
{
  "success": false,
  "error": {
    "code": "COMPILE_ERROR",
    "message": "(1,1): error CS1002: ; expected"
  }
}
```

Error codes: `INVALID_ARGS`, `INTERNAL_ERROR`, `COMPILE_ERROR`, `RUNTIME_ERROR`, `UNKNOWN_GROUP`, `UNKNOWN_COMMAND`

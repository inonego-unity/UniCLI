---
name: inonego-uni-cli-uitk
description: UI Toolkit debugger via UniCLI. Use when inspecting or debugging UITK visual trees, USS styles, layout, stylesheets, or USS classes on VisualElements.
user-invocable: false
---

# UniCLI — UI Toolkit Debugger

`unicli uitk <command> [args...] [--options]` — inspect and debug UI Toolkit visual trees.

## Commands

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `inspect` | | `--window <id>` (required), `--depth <n>`, `--path`, `--style [prop...]`, `--layout`, `--sheet` | Dump visual tree (default depth 1) |
| `query` | `<selector>` | `--window <id>` (required) | Find elements by CSS selector (#name, .class, Type) |
| `class add` | `<class>` | `--window <id>` (required), `--path` (required) | Add USS class |
| `class remove` | `<class>` | `--window <id>` (required), `--path` (required) | Remove USS class |
| `class toggle` | `<class>` | `--window <id>` (required), `--path` (required) | Toggle USS class |

`--window <id>`: use `unicli editor window list` to get instance IDs.
`--path <index-path>`: identify elements by hierarchy index path (e.g. `0/2/1` = root→child[0]→child[2]→child[1]).
`--style`: no value = all resolvedStyle; with values = selected only (`--style color --style font_size`). USS snake_case property names.
`--layout`: layout Rect (parent-relative) + worldBound (window-relative).
`--sheet`: USS stylesheets directly attached to the element.

## jq Patterns

```bash
# Summarize tree elements
unicli uitk inspect --depth 2 | jq '.. | .element? // empty | {type, name, index_path}'

# Find named elements only
unicli uitk inspect --depth -1 | jq '.. | .element? // empty | select(.name != null) | {name, index_path}'

# Get index_paths from query
unicli uitk query ".my-class" | jq '.result[].element.index_path'

# Filter style properties
unicli uitk inspect --path 0/2/1 --style | jq '.result.resolved | {color, background_color, font_size}'

# Children layout
unicli uitk inspect --path 0/2 --layout --depth 1 | jq '.result.children[].layout'

# Stylesheet paths
unicli uitk inspect --path 0 --sheet | jq '.. | .stylesheets? // empty | .[].path'
```

## Workflows

```bash
# USS not applying → check class → check style → check sheet
unicli uitk inspect --depth 2 | jq '.. | .element? // empty | select(.classes | index(".my-class")) | .index_path'
unicli uitk inspect --path 0/2/1 --style color --style background_color
unicli uitk inspect --path 0/2/1 --sheet | jq '.result.stylesheets[].path'

# Wrong layout → find container → check geometry + flex
unicli uitk query "#my-container" | jq '.result[0].element.index_path'
unicli uitk inspect --path 0/2 --layout --depth 1 | jq '.result | {layout}, (.children[] | {name: .element.name, layout})'
unicli uitk inspect --path 0/2 --style flex_direction --style flex_grow --style align_items

# Element not visible → check display/visibility/opacity
unicli uitk inspect --path 0/2/1 --style display --style visibility --style opacity

# Live class debugging → remove and compare
unicli uitk inspect --path 0/2/1 --style color
unicli uitk class remove my-class --window -12340 --path 0/2/1
unicli uitk inspect --path 0/2/1 --style color
unicli uitk class add my-class --window -12340 --path 0/2/1
```

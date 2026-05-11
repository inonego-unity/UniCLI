# UI Toolkit Debugger (UITK)

UI Toolkit visual tree inspection, USS styling, layout debugging, and stylesheets management.

## Usage

```bash
unicli uitk <command> [args...]
```

---

## tree — Visual Tree

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `list` | `<panel_id>` | `--depth <n>` | List visual tree (optionally with depth limit) |
| `get` | `<element_id>` | | Get element details (properties, styles, layout) |
| `find` | `<selector>` | `--class <c>`, `--name <n>` | Find elements by CSS selector or attributes |
| `select` | `<element_id>` | | Highlight element in UITK debugger |

---

## style — USS Styling

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `computed` | `<element_id>` | | Get computed styles (resolved values) |
| `inline` | `<element_id>` | | Get inline styles |
| `classes` | `<element_id>` | | List applied USS classes |
| `sheet list` | `[panel_id]` | | List stylesheets (all or for specific panel) |
| `sheet get` | `<sheet_id>` | | Get stylesheet content |

---

## layout — Layout Debugging

| Command | Args | Options | Description |
|---------|------|---------|-------------|
| `bounds` | `<element_id>` | | Get element bounds (rect, local, world) |
| `measure` | `<element_id>` | | Get measured size (layout cache) |
| `hierarchy` | `<element_id>` | | Get layout hierarchy from element upward |

---

## Common Queries

```bash
# Find all buttons
unicli uitk tree find "Button"

# List visual tree with depth limit (useful for large panels)
unicli uitk tree list <panel_id> --depth 3

# Get computed styles for debugging layout issues
unicli uitk style computed <element_id>

# Check applied USS classes
unicli uitk style classes <element_id>

# Find elements by class
unicli uitk tree find "" --class "selected"

# Find elements by name
unicli uitk tree find "" --name "SubmitButton"
```

---

## Integration with eval

For deeper UITK inspection, combine with `unicli eval`:

```bash
# Get all VisualElements matching a query
unicli eval lua 'return CS.UnityEngine.UIElements.UQuery.Q("MyElement", "panel-main").Query().ToList()'

# Check element state programmatically
unicli eval cs 'var el = <element_instance>; return el.resolvedStyle.width;'
```

---

## Output Format

Visual tree elements include:
- `element_id` — unique identifier
- `name` — element name
- `classes` — CSS classes applied
- `bounds` — computed layout bounds
- `computed_styles` — resolved USS values
- `children` — child elements (recursive)

Styles include:
- `property` — USS property name
- `value` — resolved value
- `source` — stylesheet/inline origin

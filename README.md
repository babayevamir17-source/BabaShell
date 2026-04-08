# BabaShell

BabaShell is a human-friendly scripting language and runtime for building interactive web behavior with simple syntax, plus backend-style automation features (HTTP, JSON, files, crypto, and bot/webhook helpers).

It is designed to feel easier than JavaScript while keeping practical power for real projects.

## Highlights

- Clean scripting syntax:
  - `store`, `if/else`, `repeat`, `for in`, `func`, `call`, `wait`, `fetch`
- DOM + CSS control:
  - `set #id text "..."`, `set #id background-color "red"`
  - selector events: `when #btn clicked { ... }`, `when .card hover { ... }`
- HTML/CSS binding:
  - `use from {./index.html}`
  - `use from {./theme.css}`
- CSS namespace syntax:
  - `theme.#card.width: 320px`
  - `theme.background-color: #111`
- Rich standard library:
  - core, string, array/map, filesystem, network, crypto, time
  - module-style namespaces: `math`, `str`, `arr`, `obj`, `json`, `net`, `bot`, `crypto`

## CLI

```bash
babashell run app.babashell
babashell serve app.babashell 3000
babashell export app.babashell out.html
babashell --check app.babashell
```

## Quick Start

### 1) Web script (`app.babashell`)

```baba
use from {./index.html}
use from {./style.css}

store count = 0
set #out text "Ready"

when #btn clicked {
    increase count by 1
    set #out text ("Clicks: " + count)
    style.#btn.background-color: #2563eb
}

when #card hover {
    set #card style="transform:scale(1.02);transition:0.2s"
}
```

### 2) HTML (`index.html`)

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>BabaShell Demo</title>
</head>
<body>
  <button id="btn">Click</button>
  <div id="out"></div>
  <div id="card">Hover me</div>
  <!-- BABASHELL -->
</body>
</html>
```

### 3) Serve

```bash
babashell serve app.babashell 3000
```

Open:

- `http://127.0.0.1:3000/`
- `http://localhost:3000/`

## Language Reference

### State

```baba
store name = "Amir"
store score = 0
increase score by 1
decrease score by 1
```

### Functions

```baba
func greet(name) {
    return "Hello " + name
}

call greet("Amir")
```

### Conditions and Loops

```baba
if score > 10 {
    emit "Win"
} else {
    emit "Lose"
}

when score > 10 {
    emit "Win"
}

repeat 5 times {
    emit "tick"
}

for item in [1,2,3] {
    emit item
}
```

### Async and API

```baba
wait 2s {
    set #out text "Done"
}

fetch "https://api.example.com/user" as data {
    set #out text data.name
}
```

## Standard Library

### Direct helpers

- Core: `print`, `random`, `length`, `type_of`, `parse_number`, `to_string`
- String: `lower`, `upper`, `trim`, `contains`, `starts_with`, `ends_with`, `replace`, `split`, `join`, `slice`, `regex_is_match`
- Collections: `push`, `pop`, `shift`, `unshift`, `keys`, `values`, `has_key`
- Files/dirs: `file_read`, `file_write`, `file_append`, `file_exists`, `file_delete`, `file_copy`, `file_move`, `dir_exists`, `dir_make`, `dir_delete`, `dir_list`
- Network: `http_get`, `http_post_json`, `json_parse`, `json_stringify`
- Bot: `discord_webhook_send`
- Crypto: `hash_sha256`, `hash_md5`, `base64_encode`, `base64_decode`
- Time: `now`, `unix_time`, `format_time`

### Namespaced modules

- `math.*`
- `str.*`
- `arr.*`
- `obj.*`
- `json.*`
- `net.*`
- `bot.*`
- `crypto.*`

Example:

```baba
store result = math.sqrt(144)
store ok = bot.discord_webhook_send("https://discord.com/api/webhooks/...", "Hello from BabaShell")
```

## VS Code Extension

The extension provides:

- Syntax highlight
- Diagnostics (`--check`)
- Run command integration
- CSS-aware autocomplete for `set` lines
- Snippets for web, API, and bot workflows

Set executable path if needed:

```json
"babashell.executablePath": "C:\\Program Files\\BabaShell\\babaSHELL.exe"
```

## Notes

- If `localhost` has issues on your machine, use `127.0.0.1`.
- `serve` prints LAN URLs so you can test from other devices in the same network.

## License

Add your preferred license file (`LICENSE`) for open-source distribution.


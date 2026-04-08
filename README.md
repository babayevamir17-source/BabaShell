# BabaShell

BabaShell is a scripting language and runtime built in C# for people who want web behavior, automation, modules, and scripting power without writing raw JavaScript for everything.

It is not positioned as a toy syntax wrapper. The current engine includes:

- a lexer
- a parser
- an AST interpreter
- semantic analysis before execution
- a web runtime/compiler for HTML-bound scripts
- a CLI and VS Code extension

## What BabaShell Does

- Bind logic to HTML and CSS with `use from`
- Control the DOM with readable scripting syntax
- Run scripts in CLI mode
- Serve live web projects locally
- Export standalone HTML output
- Work with files, directories, HTTP, JSON, crypto, time, and Discord webhooks
- Split code into modules with `import` and `export`
- Build object-oriented scripts with `class`, `new`, and `this`

## Current Language Shape

### State and math

```baba
store score = 0
score += 1
score *= 2
score = score + 3
```

### Conditions and loops

```baba
if score > 10 {
    emit "Win"
} else if score > 5 {
    emit "Close"
} else {
    emit "Lose"
}

while score < 20 {
    score += 1
    if score == 15 {
        continue
    }
    if score == 18 {
        break
    }
}
```

### Functions

```baba
func greet(name) {
    return "Hello " + name
}

emit greet("Amir")
```

### Classes

```baba
class Person {
    func init(name) {
        this.name = name
    }

    func greet() {
        return "Hello " + this.name
    }
}

store user = new Person("Amir")
emit user.greet()
```

### Modules

`mathlib.babashell`

```baba
export func add(a, b) {
    return a + b
}

export store pi = 3.14159
```

`app.babashell`

```baba
import mathlib

emit mathlib.add(2, 3)
emit mathlib.pi
```

### HTML and CSS binding

```baba
use from {./index.html}
use from {./style.css}

set #out text "Ready"

when #btn clicked {
    set #out text "Clicked"
    set #btn background-color "#2563eb"
    style.#btn.color: white
}

when .card hover {
    set .card style="transform:scale(1.02)"
}
```

## CLI

```bash
babashell run app.babashell
babashell app.babashell
babashell serve app.babashell 3000
babashell export app.babashell out.html
babashell --check app.babashell
babashell --version
```

If you run `babashell` with no arguments, it opens the interactive REPL.

## Interactive Terminal

The CLI now has a proper terminal banner and structured help output.

When you open the REPL with:

```bash
babashell
```

you get:

- a branded startup screen
- a quick command overview
- a cleaner entry into the REPL

Inside the REPL:

- `help` prints command and feature guidance
- `exit`, `quit`, or `:q` leaves the REPL

## Standard Library

### Core

- `help`
- `print`
- `input`
- `confirm`
- `ask_number`
- `choose`
- `clear`
- `random`
- `length`
- `size`
- `type_of`
- `parse_number`
- `to_string`

### Strings

- `lower`
- `upper`
- `trim`
- `contains`
- `starts_with`
- `ends_with`
- `replace`
- `split`
- `join`
- `slice`
- `regex_is_match`

### Collections

- `push`
- `pop`
- `shift`
- `unshift`
- `keys`
- `values`
- `has_key`

### File system

- `file_read`
- `file_write`
- `file_append`
- `file_exists`
- `file_delete`
- `file_copy`
- `file_move`
- `dir_exists`
- `dir_make`
- `dir_delete`
- `dir_list`

### Network and JSON

- `http_get`
- `http_post_json`
- `json_parse`
- `json_stringify`

### Bot and crypto

- `discord_webhook_send`
- `hash_sha256`
- `hash_md5`
- `base64_encode`
- `base64_decode`

### Time

- `now`
- `unix_time`
- `format_time`

### Namespaced builtins

- `math.*`
- `str.*`
- `arr.*`
- `obj.*`
- `json.*`
- `net.*`
- `bot.*`
- `crypto.*`

## Semantic Analysis

BabaShell now validates more than syntax before execution.

Current checks include:

- undefined variable usage
- duplicate declarations in the same scope
- `break` outside loops
- `continue` outside loops
- `return` outside functions
- `this` outside class methods

That means `--check` is now materially useful:

```bash
babashell --check app.babashell
```

## VS Code Extension

The extension currently provides:

- syntax highlighting
- diagnostics through `--check`
- run integration
- snippets
- CSS-aware autocomplete for `set` and stylesheet-style property usage

If the executable path is not auto-detected:

```json
"babashell.executablePath": "C:\\Program Files\\BabaShell\\babaSHELL.exe"
```

## Project Structure

```text
babaSHELL/
  Ast.cs
  Lexer.cs
  Parser.cs
  SemanticAnalyzer.cs
  Interpreter.cs
  BabaEnvironment.cs
  Builtins.cs
  BabaRunner.cs
  BabaServer.cs
  BabaExporter.cs
  BabaRuntime.cs

vscode/babashell/
  src/
  syntaxes/
  snippets/
  themes/
```

## Recent Major Upgrades

### v1.5.0

- semantic analysis before execution
- stronger scope validation
- clearer CLI safety for invalid control flow

### v1.4.0

- explicit module exports
- cached imports

### v1.3.0

- `class`
- `new`
- `this`
- constructors via `init`

### v1.2.1

- compound math assignment:
  - `+=`
  - `-=`
  - `*=`
  - `/=`
  - `%=`

### v1.1.0

- proper `if / else if / else`
- event-only `when`
- interactive input functions

## Direction

The next serious architecture steps are:

1. typed semantic analysis
2. stronger member binding validation
3. compiler lowering
4. bytecode VM
5. extensible native module/plugin loading

Those are the changes that turn a scripting project into a language engine that scales.

## License

MIT. See `LICENSE`.

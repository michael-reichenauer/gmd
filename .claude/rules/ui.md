---
paths:
  - "gmd/Cui/**"
  - "gmdE2eTest/**"
  - "scripts/demo"
---

# The UI: decisions, and looking at it by hand

## Confirmations

How hard a question guards an action follows from what a slip costs (USABILITY.md, "Safe defaults"):

- Harmless but disruptive (quitting, a gesture such as the middle-click merge): ask, with the action
  as the default. Esc in the log view asks "Quit gmd?" with Yes the default, so Esc, Enter quits at
  once while a second Esc, the likely slip, answers No; `q` quits without asking
  (`RepoViewInput.QuitAfterAsking`).
- Irreversible loss (discard changes, drop a stash, remove a tag on origin): No or Cancel is the
  default, and the question says what will be lost.
- When asking the user how a new question should behave, offer "ask, with the action the default"
  among the options: a question to them that lacked it was turned down.

## Driving gmd by hand in tmux

CLAUDE.md, "Running the TUI from a non-interactive shell", has the basics.

- **A slow or fake git for a hand experiment**, e.g. to see what the UI shows while a command runs:
  put a `git` script beside the built binary (`<copy>/gmd/bin/Debug/net10.0/git`), since .NET's
  `Process` looks for a bare file name in the executable's own folder before `PATH`. A wrapper folder
  put first on `PATH` with `tmux new-session -e PATH=…` did nothing: the pane had it, but gmd's git
  was still the real one, which cost two runs that proved nothing. Delete the script before running
  tests from that copy.
- **In a test** that route is closed, and `TempRepo` sets `core.hooksPath no-hooks`: a slow commit is
  a sleeping pre-commit hook under `.git/` that `core.hooksPath` points at
  (`CommitTest.SlowDownCommitsAsync`).
- A real git login prompt is reached by hand as `docs/CONTRIBUTING.md` describes (a stand-in ssh
  through `core.sshCommand`; git calls it with `-G` first, so it exits at once for that).

## Screenshots of UI work

When they are wanted: `workflow.md`, "Showing UI work". How:

- Rebuild first: a commit checked alone with `git stash --keep-index` and a test run leaves the
  Debug binary built from that older state.
- Drive the built binary in tmux on a throwaway repo with a hermetic `HOME`, as CLAUDE.md describes,
  and take `tmux capture-pane -p -e`.
- Wrap the capture in a one-frame asciicast, a header `{"version": 2, "width": <w>, "height": <h>}`
  and one event `[0, "o", "<text>"]` whose text is `\x1b[H\x1b[2J` and the rows joined by `\r\n`,
  JSON-escaped by hand where python3 has no `json` module (the devcontainer's has none).
- tmux leaves out the color code at the start of a row when the row above ended in that color, so
  carry the color state from row to row (each row prefixed with the codes since the last reset)
  rather than resetting at each line end, or a colored first cell renders in the default color.
- Render it with agg and `scripts/demo`'s options, its font families, Campbell theme and
  `--line-height 1.32` (without which the graph's box drawing leaves gaps between rows), at
  `--font-size 14` rather than its 16. Two captures side by side in one cast make a before and
  after picture.
- Send it with `SendUserFile` (display: render).
